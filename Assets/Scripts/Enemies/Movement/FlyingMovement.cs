using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Free flight with no gravity. Steers toward the requested direction with inertia, so
    /// flyers drift and overshoot rather than snapping, and adds an idle bob so a hovering
    /// enemy is never perfectly still.
    /// </summary>
    public class FlyingMovement : MovementControllerBase
    {
        [Header("Flight")]
        [SerializeField, Tooltip("Keep the flyer within this distance of its spawn point. 0 disables tethering.")]
        private float tetherRadius;

        [SerializeField, Tooltip("Push away from terrain the flyer drifts into.")]
        private bool avoidTerrain = true;

        [SerializeField, Min(0f), Tooltip("How far ahead terrain avoidance looks.")]
        private float avoidanceDistance = 0.6f;

        private FlyingMovementSettings flyingSettings;
        private Vector2 anchor;
        private float bobPhase;

        /// <summary>Point the flyer is tethered to; set on spawn.</summary>
        public Vector2 Anchor
        {
            get => anchor;
            set => anchor = value;
        }

        public override bool IsBlockedAhead => IsTouchingWall;

        protected override void ConfigureBody()
        {
            flyingSettings = Settings as FlyingMovementSettings;

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
            anchor = transform.position;
            bobPhase = Random.value * Mathf.PI * 2f;
        }

        protected override void ApplyMotion(float fixedDeltaTime)
        {
            if (Body == null || Settings == null)
            {
                return;
            }

            if (IsControlLocked)
            {
                return;
            }

            Vector2 desired = DesiredDirection * (Settings.MoveSpeed * DesiredSpeedScale);

            if (tetherRadius > 0f)
            {
                Vector2 fromAnchor = (Vector2)transform.position - anchor;
                if (fromAnchor.magnitude > tetherRadius)
                {
                    desired += -fromAnchor.normalized * Settings.MoveSpeed;
                }
            }

            if (avoidTerrain)
            {
                desired += ComputeAvoidance() * Settings.MoveSpeed;
            }

            if (flyingSettings != null && flyingSettings.BobAmplitude > 0f)
            {
                bobPhase += flyingSettings.BobFrequency * Mathf.PI * 2f * fixedDeltaTime;
                desired.y += Mathf.Cos(bobPhase) * flyingSettings.BobAmplitude;
            }

            float response = flyingSettings != null ? flyingSettings.SteeringResponse : 6f;
            float rate = (DesiredDirection.sqrMagnitude > 0.0001f ? Settings.Acceleration : Settings.Deceleration) * Mathf.Max(0.01f, response / 6f);

            Body.linearVelocity = Vector2.MoveTowards(Body.linearVelocity, desired, rate * fixedDeltaTime);
        }

        private Vector2 ComputeAvoidance()
        {
            Vector2 origin = transform.position;
            Vector2 push = Vector2.zero;

            // Four cheap probes beat a physics overlap here, and only run once per step.
            if (ProbeRay(origin, Vector2.down, avoidanceDistance).collider != null)
            {
                push += Vector2.up;
            }

            if (ProbeRay(origin, Vector2.up, avoidanceDistance).collider != null)
            {
                push += Vector2.down;
            }

            if (ProbeRay(origin, Vector2.right, avoidanceDistance).collider != null)
            {
                push += Vector2.left;
            }

            if (ProbeRay(origin, Vector2.left, avoidanceDistance).collider != null)
            {
                push += Vector2.right;
            }

            return push;
        }

        public override bool TryDash(Vector2 direction, float force, float duration)
        {
            if (Body == null || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Body.linearVelocity = direction.normalized * force;
            LockControl(duration);
            return true;
        }
    }
}
