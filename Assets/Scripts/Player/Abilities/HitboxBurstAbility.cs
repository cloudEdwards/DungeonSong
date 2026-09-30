using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>
    /// Spawns a short-lived area in front of the player that damages everything inside it
    /// once: Burning Hands' cone of fire, and later a thunderwave or a holy nova.
    /// <para>
    /// The prefab carries a <see cref="Hitbox"/> and its trigger shape, plus whatever
    /// visuals it likes. Damage goes through the same hitbox → hurtbox path as a sword
    /// swing, so hit feedback, defenses and Loyalty gain all work with no extra code.
    /// </para>
    /// </summary>
    public class HitboxBurstAbility : AbilityBehaviour
    {
        [Header("Burst")]
        [SerializeField, Tooltip("Prefab with a Hitbox and a trigger collider, authored facing right.")]
        private GameObject burstPrefab;

        [SerializeField, Tooltip("Spawn offset from the player, for a right-facing player. Mirrored when facing left.")]
        private Vector2 offset = new Vector2(0.5f, 0.6f);

        [SerializeField, Tooltip("Parent the burst to the player so it moves with them.")]
        private bool followPlayer = true;

        [SerializeField, Min(0f), Tooltip("Seconds the hitbox can deal damage.")]
        private float activeSeconds = 0.2f;

        [SerializeField, Min(0.05f), Tooltip("Seconds before the burst object is destroyed. Cover the visuals.")]
        private float lifetime = 0.65f;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float damage = 20f;

        [SerializeField, Min(0f)] private float poiseDamage = 15f;

        [SerializeField] private DamageType damageType = DamageType.Fire | DamageType.Magic;

        [SerializeField, Min(0f)] private float knockbackForce = 4f;

        protected override void OnResolve(in TargetInfo target)
        {
            if (burstPrefab == null)
            {
                Debug.LogWarning($"HitboxBurstAbility on '{name}' has no burst prefab; nothing was cast.", this);
                return;
            }

            int facing = Owner.FacingDirection >= 0 ? 1 : -1;
            Vector3 position = transform.position + new Vector3(offset.x * facing, offset.y, 0f);

            GameObject burst = Instantiate(burstPrefab, position, Quaternion.identity, followPlayer ? transform : null);

            // Mirror by scale, so the cone, its collider and its flames all flip together.
            Vector3 scale = burst.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facing;
            burst.transform.localScale = scale;

            var hitbox = burst.GetComponentInChildren<Hitbox>();
            if (hitbox != null)
            {
                hitbox.TargetTeams = Owner.HostileTeams;

                DamageInfo info = DamageInfo.Create(damage, Owner.Team, position, Owner.gameObject);
                info.Type = damageType;
                info.PoiseDamage = poiseDamage;
                info.KnockbackForce = knockbackForce;
                hitbox.Activate(in info);

                if (activeSeconds < lifetime)
                {
                    StartCoroutine(CloseAfter(hitbox, activeSeconds));
                }
            }
            else
            {
                Debug.LogWarning($"Burst prefab '{burstPrefab.name}' has no Hitbox; it will deal no damage.", burstPrefab);
            }

            Destroy(burst, lifetime);
        }

        /// <summary>Closes the hitbox early, so the visuals can linger harmlessly.</summary>
        private static System.Collections.IEnumerator CloseAfter(Hitbox hitbox, float seconds)
        {
            yield return new WaitForSeconds(seconds);

            if (hitbox != null)
            {
                hitbox.Deactivate();
            }
        }
    }
}
