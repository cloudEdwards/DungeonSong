using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Crawls along whatever surface it is stuck to, rotating around convex corners and
    /// climbing into concave ones, so one component covers floor, wall and ceiling travel.
    /// <para>
    /// It drives the rigidbody directly instead of relying on gravity, and owns its own
    /// facing (the enemy's <see cref="FacingMode"/> should be None), because "left" means
    /// something different on a ceiling.
    /// </para>
    /// </summary>
    public class SurfaceMovement : MovementControllerBase
    {
        [Header("Surface")]
        [SerializeField, Tooltip("Direction along the surface to start crawling. +1 or -1.")]
        private int initialTravelSign = 1;

        [SerializeField, Tooltip("Rotate the transform to match the surface angle.")]
        private bool alignToSurface = true;

        [Header("Detachment")]
        [SerializeField, Min(0f), Tooltip("Gravity applied while detached from any surface.")]
        private float detachedGravity = 2f;

        private SurfaceMovementSettings surfaceSettings;
        private Vector2 surfaceNormal = Vector2.up;
        private int travelSign = 1;
        private bool attached = true;

        /// <summary>Outward normal of the surface currently held.</summary>
        public Vector2 SurfaceNormal => surfaceNormal;

        /// <summary>Unit vector along the surface in the direction of travel.</summary>
        public Vector2 SurfaceTangent => -Vector2.Perpendicular(surfaceNormal) * travelSign;

        /// <summary>False while falling between surfaces.</summary>
        public bool IsAttached => attached;

        /// <summary>+1 or -1 along the surface. Flipped when the crawler turns around.</summary>
        public int TravelSign => travelSign;

        public override bool IsBlockedAhead => !attached;

        protected override void ConfigureBody()
        {
            surfaceSettings = Settings as SurfaceMovementSettings;

            if (Body == null)
            {
                return;
            }

            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.gravityScale = 0f;
            Body.freezeRotation = true;
        }

        public override void OnEnemySpawned()
        {
            base.OnEnemySpawned();
            travelSign = initialTravelSign >= 0 ? 1 : -1;
            surfaceNormal = Vector2.up;
            attached = true;
        }

        /// <summary>Turns the crawler around along its current surface.</summary>
        public void ReverseTravel() => travelSign = -travelSign;

        /// <summary>
        /// Steering for a crawler is one bit of information: which way along the surface.
        /// A world-space direction is projected onto the surface tangent.
        /// </summary>
        public override void Move(Vector2 direction, float speedScale = 1f)
        {
            base.Move(direction, speedScale);

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float along = Vector2.Dot(direction.normalized, -Vector2.Perpendicular(surfaceNormal));
            if (Mathf.Abs(along) > 0.2f)
            {
                travelSign = along > 0f ? 1 : -1;
            }
        }

        protected override void UpdateProbes()
        {
            // The stock probes assume a gravity-down world, which is exactly what a
            // crawler is not, so surface probing replaces them entirely.
            if (surfaceSettings == null)
            {
                base.UpdateProbes();
                return;
            }

            Vector2 origin = transform.position;
            Vector2 tangent = SurfaceTangent;

            // 1. Is there still a surface beneath us?
            RaycastHit2D down = ProbeRay(origin, -surfaceNormal, surfaceSettings.StickDistance);

            // 2. Is there a wall ahead to climb into (a concave corner)?
            RaycastHit2D ahead = ProbeRay(origin, tangent, surfaceSettings.ForwardProbeDistance);

            IsGrounded = down.collider != null;
            IsTouchingWall = ahead.collider != null;
            IsEdgeAhead = false;
            IsTouchingCeiling = Vector2.Dot(surfaceNormal, Vector2.down) > 0.7f && IsGrounded;

            if (ahead.collider != null)
            {
                // Concave corner: adopt the wall as the new surface and keep going.
                surfaceNormal = ahead.normal;
                attached = true;
                SnapToSurface(ahead);
                return;
            }

            if (down.collider != null)
            {
                surfaceNormal = Vector2.Lerp(surfaceNormal, down.normal, 0.5f).normalized;
                attached = true;
                SnapToSurface(down);
                return;
            }

            // Nothing below and nothing ahead: a convex corner. Rotate the normal toward
            // the direction of travel and probe again before giving up and falling.
            Vector2 rotatedNormal = tangent;
            Vector2 probeOrigin = origin + tangent * surfaceSettings.SurfaceOffset;
            RaycastHit2D around = ProbeRay(probeOrigin, -rotatedNormal, surfaceSettings.StickDistance);

            if (around.collider != null && !surfaceSettings.FallWhenDetached)
            {
                surfaceNormal = around.normal;
                attached = true;
                SnapToSurface(around);
                return;
            }

            attached = false;
            IsEdgeAhead = true;
        }

        private void SnapToSurface(RaycastHit2D hit)
        {
            if (Body == null)
            {
                return;
            }

            Vector2 target = hit.point + hit.normal * (surfaceSettings?.SurfaceOffset ?? 0.25f);
            Vector2 current = transform.position;

            // Correct only the component along the normal, so forward motion is untouched.
            Vector2 correction = Vector2.Dot(target - current, hit.normal) * hit.normal;
            Body.position = current + correction;
        }

        protected override void ApplyMotion(float fixedDeltaTime)
        {
            if (Body == null || Settings == null)
            {
                return;
            }

            if (!attached)
            {
                // Detached: fall until a surface catches us again.
                Body.linearVelocity += Vector2.down * (detachedGravity * fixedDeltaTime);
                return;
            }

            if (IsControlLocked)
            {
                return;
            }

            float speed = DesiredDirection.sqrMagnitude > 0.0001f ? Settings.MoveSpeed * DesiredSpeedScale : 0f;
            Body.linearVelocity = SurfaceTangent * speed;

            if (alignToSurface)
            {
                AlignToSurface(fixedDeltaTime);
            }
        }

        private void AlignToSurface(float fixedDeltaTime)
        {
            float alignSpeed = surfaceSettings != null ? surfaceSettings.AlignSpeed : 540f;
            float targetAngle = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, alignSpeed * fixedDeltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;

            Gizmos.color = attached ? Color.green : Color.red;
            Gizmos.DrawLine(origin, origin + (Vector3)(surfaceNormal * 0.6f));

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(origin, origin + (Vector3)(SurfaceTangent * 0.6f));
        }
    }
}
