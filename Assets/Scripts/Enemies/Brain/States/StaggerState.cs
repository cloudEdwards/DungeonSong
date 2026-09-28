using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// The punish window: the enemy has lost its poise and is open. Higher priority than
    /// hurt and non-interruptible, so it cannot be cut short by the enemy's own AI wanting
    /// to attack.
    /// </summary>
    public class StaggerState : EnemyStateBehaviour
    {
        [Header("Stagger")]
        [SerializeField, Min(0f), Tooltip("Seconds staggered. Overridden by the enemy's stats when those are present.")]
        private float duration = 0.6f;

        [SerializeField, Tooltip("Animation key played while staggered.")]
        private string staggerAnimationKey = "stagger";

        [SerializeField, Tooltip("Cancel any attack in progress when the stagger begins.")]
        private bool cancelAttacks = true;

        private bool pending;

        protected override int DefaultPriority => 90;

        public override bool WantsControl => pending;

        /// <summary>Staggers run to completion; the enemy does not get to act out of one.</summary>
        public override bool IsInterruptible => false;

        public override void OnEnemyInitialized() => Owner.Staggered += OnStaggered;

        private void OnDestroy()
        {
            if (Owner != null)
            {
                Owner.Staggered -= OnStaggered;
            }
        }

        public override void OnEnemySpawned() => pending = false;

        private void OnStaggered(Enemy enemy, DamageInfo info)
        {
            pending = true;

            // Forced: a stagger must land even while a committed attack is running.
            RequestSelf(force: true);
        }

        protected override void OnEnter()
        {
            pending = false;
            Movement?.Stop(true);
            Anim.SetLocomotionSpeed(0f);
            Anim.PlayAction(staggerAnimationKey);

            if (cancelAttacks)
            {
                Attacks?.CancelCurrentAttack();
            }
        }

        protected override void OnStateTick(float deltaTime)
        {
            float stagger = Health != null && Health.Stats != null ? Health.Stats.StaggerDuration : duration;
            if (TimeInState >= Mathf.Max(0.01f, stagger))
            {
                Finish();
            }
        }
    }
}
