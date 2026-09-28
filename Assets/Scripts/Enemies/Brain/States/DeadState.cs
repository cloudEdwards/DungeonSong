using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Owns the death sequence: play the animation, stop being solid, then despawn. The
    /// highest priority state, non-interruptible, and it never finishes.
    /// </summary>
    public class DeadState : EnemyStateBehaviour
    {
        [Header("Death")]
        [SerializeField, Min(0f), Tooltip("Seconds before despawning. Overridden by the enemy's stats when those are present.")]
        private float despawnDelay = 1.5f;

        [SerializeField, Tooltip("Animation key played on death.")]
        private string deathAnimationKey = "death";

        [Header("Cleanup")]
        [SerializeField, Tooltip("Disable colliders so the corpse stops blocking and stops taking hits.")]
        private bool disableColliders = true;

        [SerializeField, Tooltip("Make the body kinematic so it stops reacting to physics.")]
        private bool freezeBody = true;

        [SerializeField, Tooltip("Disable contact damagers, so a corpse cannot still hurt the player.")]
        private bool disableContactDamage = true;

        protected override int DefaultPriority => 1000;

        /// <summary>Wants control the moment health is gone, from any other state.</summary>
        public override bool WantsControl => Health != null && !Health.IsAlive;

        public override bool IsInterruptible => false;

        protected override void OnEnter()
        {
            Attacks?.CancelCurrentAttack();
            Movement?.Stop(true);
            Movement?.SetMotionEnabled(false);
            Anim.SetLocomotionSpeed(0f);
            Anim.PlayAction(deathAnimationKey);

            if (disableContactDamage)
            {
                var damagers = GetComponentsInChildren<Combat.ContactDamager>();
                for (int i = 0; i < damagers.Length; i++)
                {
                    damagers[i].Active = false;
                }
            }

            if (disableColliders)
            {
                var colliders = GetComponentsInChildren<Collider2D>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }
            }

            if (freezeBody && Owner.Body != null)
            {
                Owner.Body.linearVelocity = Vector2.zero;
                Owner.Body.bodyType = RigidbodyType2D.Kinematic;
            }

            float delay = Health != null && Health.Stats != null ? Health.Stats.DespawnDelay : despawnDelay;
            Owner.DespawnAfter(delay);
        }
    }
}
