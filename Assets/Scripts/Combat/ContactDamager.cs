using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Damages whatever it touches, with a per-target cooldown. This is the "walking
    /// into it hurts" case (and the spiky-shell case) kept separate from the attack
    /// system, because it is not an attack: it has no phases and no animation.
    /// </summary>
    public class ContactDamager : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField, Tooltip("Damage per contact tick.")]
        private float damage = 10f;

        [SerializeField, Tooltip("Teams that can be damaged by contact.")]
        private DamageTeam targetTeams = DamageTeam.Player;

        [SerializeField, Tooltip("Seconds before the same target can be damaged again.")]
        private float perTargetCooldown = 0.75f;

        [SerializeField, Tooltip("Knockback applied on contact. 0 disables it.")]
        private float knockbackForce = 6f;

        [SerializeField, Range(0f, 1f), Tooltip("Upward bias added to contact knockback so victims pop instead of sliding.")]
        private float knockbackUpwardBias = 0.35f;

        [Header("Source")]
        [SerializeField, Tooltip("Team this damager belongs to; it never damages its own team.")]
        private DamageTeam sourceTeam = DamageTeam.Enemy;

        [SerializeField, Tooltip("Turn off to suspend contact damage, e.g. while staggered or shelled.")]
        private bool active = true;

        private readonly Dictionary<EntityId, float> nextAllowedTime = new Dictionary<EntityId, float>();

        /// <summary>Enables or suspends contact damage without disabling the component.</summary>
        public bool Active
        {
            get => active;
            set => active = value;
        }

        public float Damage
        {
            get => damage;
            set => damage = value;
        }

        private void OnCollisionStay2D(Collision2D collision) => TryDamage(collision.collider);

        private void OnCollisionEnter2D(Collision2D collision) => TryDamage(collision.collider);

        private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

        private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);

        private void TryDamage(Collider2D other)
        {
            if (!active || other == null)
            {
                return;
            }

            IDamageable target = ResolveTarget(other, out Hurtbox hurtbox);
            if (target == null || !target.IsAlive || (target.Team & targetTeams) == 0)
            {
                return;
            }

            EntityId id = target.Transform.GetEntityId();
            if (nextAllowedTime.TryGetValue(id, out float allowedAt) && Time.time < allowedAt)
            {
                return;
            }

            nextAllowedTime[id] = Time.time + perTargetCooldown;

            DamageInfo info = DamageInfo.Create(damage, sourceTeam, transform.position, gameObject);
            info.Type = DamageType.Contact;
            info.AimKnockbackFrom(target.Transform.position, knockbackForce, knockbackUpwardBias);

            if (hurtbox != null)
            {
                hurtbox.Receive(info);
            }
            else
            {
                target.TakeDamage(in info);
            }
        }

        private static IDamageable ResolveTarget(Collider2D other, out Hurtbox hurtbox)
        {
            if (other.TryGetComponent(out hurtbox))
            {
                return hurtbox.Owner;
            }

            // Any other trigger is a sword or a sensor, not a body: without this, the player's
            // down-slash overlapping an enemy let that enemy's contact damage hit the player.
            if (other.isTrigger)
            {
                return null;
            }

            // Actors without an explicit hurtbox still take contact damage through their body.
            return other.GetComponentInParent<IDamageable>();
        }
    }
}
