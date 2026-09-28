namespace DungeonSong.Combat
{
    /// <summary>
    /// Outcome of a <see cref="IDamageable.TakeDamage"/> call. Returned by value so
    /// attackers can react to blocks, immunity and kills without querying the target.
    /// </summary>
    public struct DamageResult
    {
        /// <summary>True when any health was removed.</summary>
        public bool Applied;

        /// <summary>Damage actually removed, after defenses.</summary>
        public float AmountApplied;

        /// <summary>The hit was rejected outright (invulnerable, dead, wrong team).</summary>
        public bool Immune;

        /// <summary>A blocking defense absorbed or reduced the hit.</summary>
        public bool Blocked;

        /// <summary>The receiver lost its poise and should enter a stagger state.</summary>
        public bool Staggered;

        /// <summary>This hit reduced the receiver to zero health.</summary>
        public bool Killed;

        /// <summary>Damage the receiver pushed back onto the attacker (spikes, parries).</summary>
        public float ReflectedDamage;

        public HitReaction Reaction;

        public static DamageResult Ignored => new DamageResult { Immune = true, Reaction = HitReaction.Immune };
    }
}
