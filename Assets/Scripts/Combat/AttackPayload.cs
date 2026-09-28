using System;
using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// The damage half of an attack, as reusable serializable data.
    /// <para>
    /// This is the piece the player and the enemies genuinely must agree on, so it lives
    /// here rather than in either side's own assembly. An attack definition on either side
    /// embeds one of these and turns it into a <see cref="DamageInfo"/> at swing time.
    /// </para>
    /// </summary>
    [Serializable]
    public struct AttackPayload
    {
        [Min(0f), Tooltip("Damage before the receiver's defenses run.")]
        public float Damage;

        [Min(0f), Tooltip("Poise damage. Drives whether the hit staggers its victim.")]
        public float PoiseDamage;

        [Tooltip("What kind of damage this is. Drives resistances and future status effects.")]
        public DamageType Type;

        [Tooltip("Modifiers that bypass parts of the victim's defenses.")]
        public DamageFlags Flags;

        [Min(0f), Tooltip("Knockback impulse applied to the victim.")]
        public float KnockbackForce;

        [Range(0f, 1f), Tooltip("Upward bias added to knockback, so victims pop rather than slide.")]
        public float KnockbackUpwardBias;

        [Min(0f), Tooltip("Seconds of hit-stop on a successful hit. 0 disables it. Small values (0.04-0.08) read as weight; large values feel like a bug.")]
        public float HitStopSeconds;

        /// <summary>A sensible starting point for a light melee hit.</summary>
        public static AttackPayload Default => new AttackPayload
        {
            Damage = 10f,
            PoiseDamage = 10f,
            Type = DamageType.Physical,
            KnockbackForce = 5f,
            KnockbackUpwardBias = 0.2f,
            HitStopSeconds = 0.05f,
        };

        /// <summary>
        /// Builds the runtime damage event. <paramref name="origin"/> is where the hit came
        /// from, which directional defenses test against.
        /// </summary>
        public DamageInfo ToDamageInfo(DamageTeam sourceTeam, Vector2 origin, GameObject attacker, int attackId = 0)
        {
            DamageInfo info = DamageInfo.Create(Damage, sourceTeam, origin, attacker);
            info.PoiseDamage = PoiseDamage > 0f ? PoiseDamage : Damage;
            info.Type = Type == DamageType.None ? DamageType.Physical : Type;
            info.Flags = Flags;
            info.KnockbackForce = KnockbackForce;
            info.AttackId = attackId;
            return info;
        }
    }
}
