using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Turns on a <see cref="Hitbox"/> for the attack's active frames. Every close-range
    /// attack in the game is this component plus an <see cref="AttackDefinition"/>: a claw
    /// swipe, a sword slash, a shield bash, a body slam, a lunging bite.
    /// </summary>
    public class MeleeAttack : AttackBehaviour
    {
        [Header("Melee")]
        [SerializeField, Tooltip("Hitbox to activate. Leave empty to find one matching the definition's hitbox key.")]
        private Hitbox hitbox;

        [SerializeField, Tooltip("Teams this attack can damage.")]
        private DamageTeam targetTeams = DamageTeam.Player;

        [Header("Feedback")]
        [SerializeField, Tooltip("Animation key played when this attack connects. Leave empty for none.")]
        private string hitConfirmAnimationKey = "";

        public override void OnEnemyInitialized()
        {
            if (hitbox == null)
            {
                hitbox = FindHitboxByKey(Definition != null ? Definition.HitboxKey : null);
            }

            if (hitbox == null)
            {
                Debug.LogWarning($"MeleeAttack on '{name}' found no Hitbox for key '{Definition?.HitboxKey}'; it will deal no damage.", this);
                return;
            }

            hitbox.TargetTeams = targetTeams;
            hitbox.Hit += OnHitboxHit;
        }

        private void OnDestroy()
        {
            if (hitbox != null)
            {
                hitbox.Hit -= OnHitboxHit;
            }
        }

        private Hitbox FindHitboxByKey(string key)
        {
            var candidates = GetComponentsInChildren<Hitbox>(true);

            if (string.IsNullOrEmpty(key))
            {
                return candidates.Length > 0 ? candidates[0] : null;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].Key == key)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        protected override void OnActiveBegin()
        {
            if (hitbox == null)
            {
                return;
            }

            // Put the hitbox on the side the enemy is facing before opening it.
            ResolveAttachment(hitbox.transform);

            DamageInfo info = BuildDamageInfo();
            hitbox.Activate(in info);
        }

        protected override void OnActiveEnd() => hitbox?.Deactivate();

        private void OnHitboxHit(Hurtbox victim, DamageResult result)
        {
            if (result.Applied && !string.IsNullOrEmpty(hitConfirmAnimationKey))
            {
                Anim.PlayAction(hitConfirmAnimationKey);
            }
        }
    }
}
