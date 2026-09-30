using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>
    /// Fills a resource whenever the player lands a hit on something hostile: Loyalty, in
    /// the way Silksong's silk fills from striking enemies.
    /// <para>
    /// It listens to <see cref="CombatEvents.DamageDealt"/> rather than to any one attack,
    /// so melee, spells, projectiles and anything added later all count without opting in.
    /// </para>
    /// </summary>
    public class ResourceOnHit : PlayerModule
    {
        [Header("Gain")]
        [SerializeField, Tooltip("Resource filled by landing hits.")]
        private ResourceDefinition resource;

        [SerializeField, Min(0f), Tooltip("Amount gained per hurtbox hit. A cone hitting three enemies gains three times this.")]
        private float amountPerHit = 10f;

        [SerializeField, Tooltip("Also count hits a shield or guard absorbed.")]
        private bool countBlockedHits;

        private void OnEnable() => CombatEvents.DamageDealt += OnDamageDealt;

        private void OnDisable() => CombatEvents.DamageDealt -= OnDamageDealt;

        private void OnDamageDealt(in DamageInfo info, in DamageResult result, Hurtbox victim)
        {
            if (Owner == null || !Owner.IsAlive || info.Attacker != Owner.gameObject)
            {
                return;
            }

            if (!result.Applied && !(countBlockedHits && result.Blocked))
            {
                return;
            }

            if (victim == null || (victim.Team & Owner.HostileTeams) == 0)
            {
                return;
            }

            Owner.Resources?.Restore(resource, amountPerHit);
        }
    }
}
