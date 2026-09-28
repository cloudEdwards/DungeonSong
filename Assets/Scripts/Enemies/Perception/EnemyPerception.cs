using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Detection, line of sight, aggro and memory.
    /// <para>
    /// Targets come from <see cref="TargetRegistry"/> rather than a physics overlap, so
    /// the common case is one distance check per evaluation. Evaluations are throttled to
    /// <see cref="PerceptionSettings.TickInterval"/>, and the line-of-sight raycast only
    /// runs for a candidate that already passed distance and view-cone tests. That keeps
    /// a screen full of enemies down to a handful of raycasts per frame.
    /// </para>
    /// </summary>
    public class EnemyPerception : EnemyModule, IPerception
    {
        [Header("Settings")]
        [SerializeField, Tooltip("Overrides the perception settings on the Enemy's definition.")]
        private PerceptionSettings settingsOverride;

        [Header("Wiring")]
        [SerializeField, Tooltip("Origin for sight tests. Leave empty to use this transform.")]
        private Transform eye;

        [Header("Debug")]
        [SerializeField, Tooltip("Draw detection ranges and the sight line while selected.")]
        private bool drawGizmos = true;

        // Sight is blocked by solid geometry only. Trigger colliders (room gates, the
        // player's own ground and wall sensors) sit on ordinary layers and would otherwise
        // silently break line of sight.
        private static readonly List<RaycastHit2D> SightBuffer = new List<RaycastHit2D>(8);
        private ContactFilter2D sightFilter;
        private bool sightFilterReady;

        private PerceptionSettings settings;
        private ITargetable target;
        private Vector2 lastKnownPosition;
        private float tickTimer;
        private float timeSinceSeen;
        private bool hasLineOfSight;
        private bool forced;

        public event Action<ITargetable> TargetAcquired;

        public event Action<ITargetable> TargetLost;

        public override int TickOrder => ModuleTickOrder.Perception;

        public ITargetable CurrentTarget => target;

        public bool HasTarget => target != null;

        public bool HasLineOfSight => hasLineOfSight;

        public Vector2 LastKnownPosition => lastKnownPosition;

        public float TimeSinceSeen => timeSinceSeen;

        public bool IsSearching => target == null && settings != null && timeSinceSeen > 0f && timeSinceSeen < settings.MemoryDuration;

        public PerceptionSettings Settings => settings;

        /// <summary>Distance to the current target, or infinity without one.</summary>
        public float DistanceToTarget => target != null
            ? Vector2.Distance(EyePosition, target.AimPosition)
            : float.PositiveInfinity;

        private Vector2 EyePosition => eye != null ? eye.position : transform.position;

        protected override void OnBind()
        {
            settings = settingsOverride != null
                ? settingsOverride
                : Owner.Definition != null ? Owner.Definition.Perception : null;

            if (settings == null)
            {
                Debug.LogWarning($"EnemyPerception on '{name}' has no PerceptionSettings; it will never detect anything.", this);
            }
        }

        private void OnEnable() => NoiseEvents.NoiseEmitted += OnNoise;

        private void OnDisable() => NoiseEvents.NoiseEmitted -= OnNoise;

        public override void OnEnemySpawned()
        {
            target = null;
            forced = false;
            hasLineOfSight = false;

            // "Never seen anything", not "saw something just now". Starting at zero made
            // IsSearching true for MemoryDuration seconds after every spawn, so enemies
            // began life hunting a target they had never seen.
            timeSinceSeen = float.MaxValue;
            lastKnownPosition = transform.position;

            // Stagger the first evaluation so enemies spawned together do not all
            // evaluate on the same frame for the rest of their lives.
            tickTimer = settings != null ? UnityEngine.Random.Range(0f, settings.TickInterval) : 0f;
        }

        public override void Tick(float deltaTime)
        {
            if (settings == null)
            {
                return;
            }

            if (timeSinceSeen < float.MaxValue)
            {
                timeSinceSeen += deltaTime;
            }

            tickTimer -= deltaTime;
            if (tickTimer > 0f)
            {
                return;
            }

            tickTimer = settings.TickInterval;
            Evaluate();
        }

        private void Evaluate()
        {
            if (target != null)
            {
                EvaluateExistingTarget();
                return;
            }

            ITargetable candidate = TargetRegistry.FindNearest(EyePosition, Owner.HostileTeams, settings.DetectionRadius);
            if (candidate == null)
            {
                return;
            }

            if (!CanPerceive(candidate, out bool visible))
            {
                return;
            }

            Acquire(candidate, visible);
        }

        private void EvaluateExistingTarget()
        {
            if (!target.IsValidTarget || target.Transform == null)
            {
                Lose();
                return;
            }

            float distance = Vector2.Distance(EyePosition, target.AimPosition);
            hasLineOfSight = !settings.RequireLineOfSight || HasClearLine(target);

            if (hasLineOfSight && distance <= settings.LoseRadius)
            {
                timeSinceSeen = 0f;
                lastKnownPosition = target.AimPosition;
                return;
            }

            // Out of sight or out of range: remember it for a while before giving up.
            if (forced)
            {
                return;
            }

            if (distance > settings.LoseRadius || timeSinceSeen > settings.MemoryDuration)
            {
                Lose();
            }
        }

        /// <summary>
        /// Distance, view cone and line of sight, in that order, so the expensive test runs last.
        /// </summary>
        private bool CanPerceive(ITargetable candidate, out bool visible)
        {
            visible = false;
            Vector2 eyePosition = EyePosition;
            Vector2 toTarget = candidate.AimPosition - eyePosition;
            float distance = toTarget.magnitude;

            if (distance > settings.DetectionRadius)
            {
                return false;
            }

            bool atContact = settings.AlwaysNoticeAtContact && distance <= settings.ContactRadius;

            if (!atContact && settings.FieldOfViewDegrees < 360f)
            {
                var facing = new Vector2(Owner.FacingDirection, 0f);
                if (Vector2.Angle(facing, toTarget.normalized) > settings.FieldOfViewDegrees * 0.5f)
                {
                    return false;
                }
            }

            if (settings.RequireLineOfSight && !atContact && !HasClearLine(candidate))
            {
                return false;
            }

            visible = true;
            return true;
        }

        private bool HasClearLine(ITargetable candidate)
        {
            Vector2 eyePosition = EyePosition;
            Vector2 toTarget = candidate.AimPosition - eyePosition;
            float distance = toTarget.magnitude;

            if (distance < 0.01f)
            {
                return true;
            }

            if (!sightFilterReady)
            {
                sightFilter = new ContactFilter2D
                {
                    useTriggers = false,
                    useLayerMask = true,
                    layerMask = settings.ObstructionMask,
                };
                sightFilterReady = true;
            }

            return Physics2D.Raycast(eyePosition, toTarget / distance, sightFilter, SightBuffer, distance) == 0;
        }

        private void Acquire(ITargetable candidate, bool visible)
        {
            target = candidate;
            hasLineOfSight = visible;
            timeSinceSeen = 0f;
            lastKnownPosition = candidate.AimPosition;
            TargetAcquired?.Invoke(candidate);
        }

        private void Lose()
        {
            ITargetable previous = target;
            target = null;
            forced = false;
            hasLineOfSight = false;

            if (previous != null)
            {
                TargetLost?.Invoke(previous);
            }
        }

        public void ForceTarget(ITargetable newTarget)
        {
            if (newTarget == null)
            {
                Clear();
                return;
            }

            forced = true;
            Acquire(newTarget, true);
        }

        public void Clear()
        {
            Lose();
            timeSinceSeen = float.MaxValue;
        }

        public void Alert(Vector2 position)
        {
            lastKnownPosition = position;

            if (target == null)
            {
                // Pretend we just lost sight there, which sends the brain into a search.
                timeSinceSeen = 0.01f;
            }
        }

        private void OnNoise(Vector2 position, float radius, DamageTeam sourceTeam)
        {
            if (settings == null || settings.HearingRadius <= 0f || Owner == null)
            {
                return;
            }

            if ((sourceTeam & Owner.HostileTeams) == 0)
            {
                return;
            }

            float audible = settings.HearingRadius + radius;
            if (((Vector2)transform.position - position).sqrMagnitude > audible * audible)
            {
                return;
            }

            Alert(position);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || settings == null)
            {
                return;
            }

            Vector3 eyePosition = EyePosition;

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(eyePosition, settings.DetectionRadius);

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(eyePosition, settings.LoseRadius);

            if (settings.FieldOfViewDegrees < 360f)
            {
                int facing = Owner != null ? Owner.FacingDirection : 1;
                float half = settings.FieldOfViewDegrees * 0.5f;
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);

                for (float a = -half; a <= half; a += 15f)
                {
                    Vector3 dir = Quaternion.Euler(0f, 0f, a) * new Vector3(facing, 0f, 0f);
                    Gizmos.DrawLine(eyePosition, eyePosition + dir * settings.DetectionRadius);
                }
            }

            if (target != null)
            {
                Gizmos.color = hasLineOfSight ? Color.red : Color.grey;
                Gizmos.DrawLine(eyePosition, target.AimPosition);
            }
        }
    }
}
