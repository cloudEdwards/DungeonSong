using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Switches a defense on for a while: raise a shield, curl into a shell, brace.
    /// The defense itself knows how to mitigate damage; this state owns only the decision
    /// of when, which is the part a designer wants to tune.
    /// </summary>
    public class DefendState : EnemyStateBehaviour
    {
        [Header("Defense")]
        [SerializeField, Tooltip("Defense to switch on. Leave empty to use the first one on this enemy.")]
        private DefenseBehaviour defense;

        [SerializeField, Min(0f), Tooltip("Seconds to hold the defense.")]
        private float duration = 1.5f;

        [Header("Triggers")]
        [SerializeField, Min(0f), Tooltip("Defend when the target is closer than this. 0 ignores distance.")]
        private float triggerDistance = 2.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Defend below this fraction of max health. 0 ignores health.")]
        private float triggerHealthFraction;

        [SerializeField, Tooltip("Defend immediately after taking a hit.")]
        private bool defendAfterBeingHit;

        [SerializeField, Min(0f), Tooltip("Seconds before this state may run again. Prevents permanent turtling.")]
        private float cooldown = 3f;

        [Header("Movement")]
        [SerializeField, Tooltip("Hold position while defending.")]
        private bool stopWhileDefending = true;

        [SerializeField, Tooltip("Keep facing the target while defending, so a directional shield stays useful.")]
        private bool faceTarget = true;

        private float cooldownTimer;
        private bool hitPending;

        protected override int DefaultPriority => 65;

        public override bool CanEnter => defense != null && cooldownTimer <= 0f;

        public override bool WantsControl
        {
            get
            {
                if (defense == null || cooldownTimer > 0f)
                {
                    return false;
                }

                if (hitPending)
                {
                    return true;
                }

                if (!Owner.HasTarget)
                {
                    return false;
                }

                if (triggerHealthFraction > 0f && Health != null && Health.Normalized <= triggerHealthFraction)
                {
                    return true;
                }

                return triggerDistance > 0f && DistanceToTarget <= triggerDistance;
            }
        }

        public override void OnEnemyInitialized()
        {
            if (defense == null)
            {
                defense = GetComponent<DefenseBehaviour>();
            }

            if (defendAfterBeingHit)
            {
                Owner.Damaged += OnDamaged;
            }
        }

        private void OnDestroy()
        {
            if (Owner != null)
            {
                Owner.Damaged -= OnDamaged;
            }
        }

        public override void OnEnemySpawned()
        {
            cooldownTimer = 0f;
            hitPending = false;
        }

        private void OnDamaged(Enemy enemy, DamageInfo info, DamageResult result)
        {
            if (result.Applied)
            {
                hitPending = true;
            }
        }

        protected override void OnBackgroundTick(float deltaTime)
        {
            // The cooldown has to keep running while other states hold control.
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= deltaTime;
            }
        }

        protected override void OnEnter()
        {
            hitPending = false;

            if (stopWhileDefending)
            {
                Movement?.Stop();
                Anim.SetLocomotionSpeed(0f);
            }

            if (faceTarget)
            {
                Owner.FaceTarget();
            }

            defense.Activate(duration);
        }

        protected override void OnStateTick(float deltaTime)
        {
            if (faceTarget && Owner.HasTarget)
            {
                Owner.FaceTarget();
            }

            if (TimeInState >= duration || !defense.IsActive)
            {
                Finish();
            }
        }

        protected override void OnExit()
        {
            defense.Deactivate();
            cooldownTimer = cooldown;
        }
    }
}
