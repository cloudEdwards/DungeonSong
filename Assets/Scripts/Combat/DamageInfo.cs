using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// A single damage event. A value type passed by <c>in</c> so that the damage
    /// pipeline allocates nothing per hit, no matter how many hits are in flight.
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>Raw damage before the receiver's defenses run.</summary>
        public float Amount;

        /// <summary>Poise damage. Receivers use it to decide whether to stagger.</summary>
        public float PoiseDamage;

        public DamageType Type;
        public DamageFlags Flags;

        /// <summary>Team of the actor that produced the hit, not of the receiver.</summary>
        public DamageTeam SourceTeam;

        /// <summary>World position the hit came from. Drives directional defenses.</summary>
        public Vector2 Origin;

        /// <summary>Best-known contact point, for VFX placement.</summary>
        public Vector2 HitPoint;

        public Vector2 KnockbackDirection;
        public float KnockbackForce;

        /// <summary>Seconds of control loss requested by the attacker. 0 for none.</summary>
        public float StunDuration;

        /// <summary>Root GameObject of the attacker. May be null for world damage.</summary>
        public GameObject Attacker;

        /// <summary>
        /// Identity of the swing that produced this hit. Receivers use it to ignore
        /// repeat hits from one activation; 0 means "untracked".
        /// </summary>
        public int AttackId;

        public bool Has(DamageFlags flag) => (Flags & flag) != 0;

        /// <summary>Builds a plain physical hit. Callers overwrite what they need.</summary>
        public static DamageInfo Create(float amount, DamageTeam sourceTeam, Vector2 origin, GameObject attacker = null)
        {
            return new DamageInfo
            {
                Amount = amount,
                PoiseDamage = amount,
                Type = DamageType.Physical,
                SourceTeam = sourceTeam,
                Origin = origin,
                HitPoint = origin,
                Attacker = attacker,
            };
        }

        /// <summary>
        /// Fills <see cref="KnockbackDirection"/> by pushing away from <see cref="Origin"/>,
        /// with an optional upward bias so ground enemies pop rather than slide.
        /// </summary>
        public void AimKnockbackFrom(Vector2 receiverPosition, float force, float upwardBias = 0f)
        {
            Vector2 away = receiverPosition - Origin;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Vector2.right;
            }

            away.Normalize();
            away.y += upwardBias;
            KnockbackDirection = away.normalized;
            KnockbackForce = force;
        }
    }
}
