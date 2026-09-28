using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Shared movement plumbing: the rigidbody, the environment probes, facing, and
    /// knockback lockout.
    /// <para>
    /// Probes are evaluated once per physics step and cached. Callers may read
    /// <see cref="IsGrounded"/> and friends as often as they like without triggering a
    /// physics query, which is what keeps a room full of enemies affordable.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class MovementControllerBase : EnemyModule, IMovementController, IKnockbackReceiver
    {
        [Header("Settings")]
        [SerializeField, Tooltip("Overrides the movement settings on the Enemy's definition.")]
        private MovementSettings settingsOverride;

        [Header("Probes")]
        [SerializeField, Tooltip("Layers treated as solid ground and walls.")]
        private LayerMask terrainMask = 1 << 8;

        [SerializeField, Tooltip("Local offset of the ground probe, usually at the feet.")]
        private Vector2 groundProbeOffset = new Vector2(0f, -0.5f);

        [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.12f;

        [SerializeField, Tooltip("Local offset of the forward wall probe, usually chest height.")]
        private Vector2 wallProbeOffset = Vector2.zero;

        [SerializeField, Min(0.01f)] private float wallProbeDistance = 0.4f;

        [SerializeField, Tooltip("Local offset of the ceiling probe.")]
        private Vector2 ceilingProbeOffset = new Vector2(0f, 0.5f);

        [SerializeField, Min(0f), Tooltip("0 skips the ceiling probe entirely.")]
        private float ceilingProbeDistance;

        [SerializeField, Tooltip("How far ahead of the feet the edge probe looks.")]
        private float edgeProbeForward = 0.4f;

        [SerializeField, Min(0.01f), Tooltip("How far down the edge probe reaches before calling it a drop.")]
        private float edgeProbeDepth = 0.6f;

        [Header("Debug")]
        [SerializeField, Tooltip("Draw probes in the scene view while selected.")]
        private bool drawProbes = true;

        private float controlLockTimer;
        private bool motionEnabled = true;

        // Reused buffers plus a filter that ignores triggers: room gates, character
        // sensors and pickups sit on ordinary layers, and treating them as solid made
        // enemies "stand" on doorways and stop at invisible walls.
        private static readonly List<Collider2D> OverlapBuffer = new List<Collider2D>(8);
        private static readonly List<RaycastHit2D> RayBuffer = new List<RaycastHit2D>(8);
        private ContactFilter2D solidFilter;
        private bool filterReady;

        protected Rigidbody2D Body { get; private set; }

        /// <summary>Direction the AI last asked for, normalized. Zero when told to stop.</summary>
        protected Vector2 DesiredDirection { get; private set; }

        protected float DesiredSpeedScale { get; private set; } = 1f;

        protected LayerMask TerrainMask => terrainMask;

        public MovementSettings Settings { get; private set; }

        public override int TickOrder => ModuleTickOrder.Movement;

        public Vector2 Velocity => Body != null ? Body.linearVelocity : Vector2.zero;

        public virtual float MaxSpeed => Settings != null ? Settings.MoveSpeed : 0f;

        public bool IsGrounded { get; protected set; }

        public bool IsTouchingWall { get; protected set; }

        public bool IsTouchingCeiling { get; protected set; }

        public bool IsEdgeAhead { get; protected set; }

        public virtual bool IsBlockedAhead => IsTouchingWall;

        public bool IsControlLocked => controlLockTimer > 0f;

        public bool MotionEnabled => motionEnabled;

        protected override void OnBind()
        {
            Body = GetComponent<Rigidbody2D>();
            Settings = settingsOverride != null
                ? settingsOverride
                : Owner.Definition != null ? Owner.Definition.Movement : null;

            if (Settings == null)
            {
                Debug.LogWarning($"{GetType().Name} on '{name}' has no MovementSettings; it will not move.", this);
            }

            ConfigureBody();
        }

        /// <summary>Filter matching solid terrain only: right layers, no trigger colliders.</summary>
        protected ContactFilter2D SolidFilter
        {
            get
            {
                if (!filterReady)
                {
                    solidFilter = new ContactFilter2D
                    {
                        useTriggers = false,
                        useLayerMask = true,
                        layerMask = terrainMask,
                    };
                    filterReady = true;
                }

                return solidFilter;
            }
        }

        /// <summary>Rebuilds the solid filter. Call after changing the terrain mask at runtime.</summary>
        protected void InvalidateSolidFilter() => filterReady = false;

        /// <summary>Is there solid terrain overlapping this circle? Ignores triggers.</summary>
        protected bool ProbeCircle(Vector2 point, float radius)
        {
            return Physics2D.OverlapCircle(point, radius, SolidFilter, OverlapBuffer) > 0;
        }

        /// <summary>Nearest solid terrain hit along a ray, or a default hit when clear.</summary>
        protected RaycastHit2D ProbeRay(Vector2 origin, Vector2 direction, float distance)
        {
            int count = Physics2D.Raycast(origin, direction, SolidFilter, RayBuffer, distance);
            if (count <= 0)
            {
                return default;
            }

            RaycastHit2D best = RayBuffer[0];
            for (int i = 1; i < count; i++)
            {
                if (RayBuffer[i].distance < best.distance)
                {
                    best = RayBuffer[i];
                }
            }

            return best;
        }

        /// <summary>Applies body type, gravity and damping for this movement style.</summary>
        protected abstract void ConfigureBody();

        /// <summary>Consumes the current intent and drives the rigidbody. Called per physics step.</summary>
        protected abstract void ApplyMotion(float fixedDeltaTime);

        public override void OnEnemySpawned()
        {
            controlLockTimer = 0f;
            motionEnabled = true;
            DesiredDirection = Vector2.zero;
            ConfigureBody();
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            UpdateProbes();

            if (controlLockTimer > 0f)
            {
                controlLockTimer -= fixedDeltaTime;
            }

            if (!motionEnabled)
            {
                return;
            }

            ApplyMotion(fixedDeltaTime);
        }

        /// <summary>
        /// Refreshes every cached probe. Override to add probes; call base first.
        /// </summary>
        protected virtual void UpdateProbes()
        {
            Vector2 origin = transform.position;
            int facing = Owner != null ? Owner.FacingDirection : 1;

            IsGrounded = ProbeCircle(origin + groundProbeOffset, groundProbeRadius);

            Vector2 wallOrigin = origin + wallProbeOffset;
            IsTouchingWall = ProbeRay(wallOrigin, Vector2.right * facing, wallProbeDistance).collider != null;

            IsTouchingCeiling = ceilingProbeDistance > 0f &&
                                ProbeRay(origin + ceilingProbeOffset, Vector2.up, ceilingProbeDistance).collider != null;

            Vector2 edgeOrigin = origin + groundProbeOffset + new Vector2(edgeProbeForward * facing, 0f);
            IsEdgeAhead = IsGrounded &&
                          ProbeRay(edgeOrigin, Vector2.down, edgeProbeDepth).collider == null;
        }

        public virtual void Move(Vector2 direction, float speedScale = 1f)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                DesiredDirection = direction.normalized;
                DesiredSpeedScale = Mathf.Max(0f, speedScale);
            }
            else
            {
                DesiredDirection = Vector2.zero;
            }
        }

        public void MoveTowards(Vector2 worldPosition, float speedScale = 1f)
        {
            Move(worldPosition - (Vector2)transform.position, speedScale);
        }

        public virtual void Stop(bool immediate = false)
        {
            DesiredDirection = Vector2.zero;

            if (immediate && Body != null)
            {
                Body.linearVelocity = Vector2.zero;
            }
        }

        public void SetVelocity(Vector2 velocity)
        {
            if (Body != null)
            {
                Body.linearVelocity = velocity;
            }
        }

        public void FaceDirection(int sign) => Owner?.SetFacing(sign);

        public virtual bool TryJump(float force = 0f) => false;

        public virtual bool TryDash(Vector2 direction, float force, float duration) => false;

        public void SetMotionEnabled(bool value)
        {
            motionEnabled = value;
            if (!value)
            {
                Stop(true);
            }
        }

        public virtual void ApplyKnockback(Vector2 direction, float force, float controlLockSeconds)
        {
            if (Body == null)
            {
                return;
            }

            DesiredDirection = Vector2.zero;
            Body.linearVelocity = direction.normalized * force;

            float configured = Settings != null ? Settings.KnockbackControlLock : 0f;
            controlLockTimer = Mathf.Max(controlLockTimer, Mathf.Max(controlLockSeconds, configured));
        }

        /// <summary>Extends the control lockout, e.g. for a dash or a scripted move.</summary>
        protected void LockControl(float seconds) => controlLockTimer = Mathf.Max(controlLockTimer, seconds);

        private void OnDrawGizmosSelected()
        {
            if (!drawProbes)
            {
                return;
            }

            Vector2 origin = transform.position;
            int facing = Owner != null ? Owner.FacingDirection : 1;

            Gizmos.color = IsGrounded ? Color.green : Color.grey;
            Gizmos.DrawWireSphere(origin + groundProbeOffset, groundProbeRadius);

            Gizmos.color = IsTouchingWall ? Color.red : Color.grey;
            Vector2 wallOrigin = origin + wallProbeOffset;
            Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * facing * wallProbeDistance);

            Gizmos.color = IsEdgeAhead ? Color.yellow : Color.grey;
            Vector2 edgeOrigin = origin + groundProbeOffset + new Vector2(edgeProbeForward * facing, 0f);
            Gizmos.DrawLine(edgeOrigin, edgeOrigin + Vector2.down * edgeProbeDepth);

            if (ceilingProbeDistance > 0f)
            {
                Gizmos.color = IsTouchingCeiling ? Color.cyan : Color.grey;
                Gizmos.DrawLine(origin + ceilingProbeOffset, origin + ceilingProbeOffset + Vector2.up * ceilingProbeDistance);
            }
        }
    }
}
