using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// A brief flinch on taking a hit. Event-driven rather than polled: it asks the machine
    /// for control the moment damage lands, so the reaction is on the same frame as the hit.
    /// </summary>
    public class HurtState : EnemyStateBehaviour
    {
        [Header("Flinch")]
        [SerializeField, Min(0f), Tooltip("Seconds of flinch. Overridden by the enemy's stats when those are present.")]
        private float duration = 0.15f;

        [SerializeField, Tooltip("Stop moving during the flinch.")]
        private bool stopMovement = true;

        [SerializeField, Tooltip("Animation key played on being hurt.")]
        private string hurtAnimationKey = "hurt";

        private bool pending;

        protected override int DefaultPriority => 80;

        public override bool WantsControl => pending;

        public override void OnEnemyInitialized() => Owner.Damaged += OnDamaged;

        private void OnDestroy()
        {
            if (Owner != null)
            {
                Owner.Damaged -= OnDamaged;
            }
        }

        public override void OnEnemySpawned() => pending = false;

        private void OnDamaged(Enemy enemy, DamageInfo info, DamageResult result)
        {
            // Staggers are a separate, stronger reaction; blocks and immune hits are not
            // reactions at all.
            if (!result.Applied || result.Staggered || result.Killed)
            {
                return;
            }

            if (Health != null && Health.Stats != null && !Health.Stats.FlinchOnHit)
            {
                return;
            }

            pending = true;
            RequestSelf();
        }

        protected override void OnEnter()
        {
            pending = false;

            if (stopMovement)
            {
                Movement?.Stop();
            }

            Anim.PlayAction(hurtAnimationKey);
        }

        protected override void OnStateTick(float deltaTime)
        {
            float flinch = Health != null && Health.Stats != null ? Health.Stats.FlinchDuration : duration;
            if (TimeInState >= Mathf.Max(0.01f, flinch))
            {
                Finish();
            }
        }
    }
}
