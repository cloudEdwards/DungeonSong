using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// The running verdict of an enemy's defenses on one incoming hit. Each defense
    /// narrows this struct rather than touching health directly, so defenses stack
    /// predictably and none of them needs to know about the others.
    /// </summary>
    public struct DefenseEvaluation
    {
        /// <summary>Multiplier applied to incoming damage. 1 = unchanged, 0 = fully absorbed.</summary>
        public float Multiplier;

        /// <summary>Flat damage subtracted after the multiplier. Armour uses this.</summary>
        public float FlatReduction;

        /// <summary>Reject the hit entirely: no damage, no poise loss, no reaction.</summary>
        public bool Immune;

        /// <summary>A block or shield handled the hit. Drives block VFX and attacker recoil.</summary>
        public bool Blocked;

        public bool SuppressKnockback;

        public bool SuppressStagger;

        /// <summary>Damage pushed back onto the attacker, for spikes and parries.</summary>
        public float ReflectDamage;

        public static DefenseEvaluation Default => new DefenseEvaluation { Multiplier = 1f };

        /// <summary>Applies this verdict to a raw damage amount.</summary>
        public float Apply(float amount)
        {
            if (Immune)
            {
                return 0f;
            }

            return UnityEngine.Mathf.Max(0f, amount * Multiplier - FlatReduction);
        }
    }

    /// <summary>
    /// One defensive capability. Add several to an enemy and they all get a say, in
    /// <see cref="ModifierOrder"/>, before health is touched.
    /// </summary>
    public interface IDamageModifier
    {
        /// <summary>Lower runs first. Immunity-style defenses should run late.</summary>
        int ModifierOrder { get; }

        /// <summary>False when this defense is currently dormant.</summary>
        bool IsActive { get; }

        /// <summary>Narrow <paramref name="evaluation"/> based on the incoming hit.</summary>
        void ModifyIncoming(in DamageInfo info, ref DefenseEvaluation evaluation);
    }
}
