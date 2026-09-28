using System;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Which side of a fight an actor belongs to. Used instead of tags so that damage
    /// filtering never depends on a concrete class or a string comparison.
    /// </summary>
    [Flags]
    public enum DamageTeam
    {
        None = 0,
        Player = 1 << 0,
        Enemy = 1 << 1,
        Neutral = 1 << 2,
        Environment = 1 << 3,
        All = Player | Enemy | Neutral | Environment,
    }

    /// <summary>
    /// Category of a damage event. Kept as flags so resistances and future status
    /// effects can test membership without a parallel enum.
    /// </summary>
    [Flags]
    public enum DamageType
    {
        None = 0,
        Physical = 1 << 0,
        Contact = 1 << 1,
        Projectile = 1 << 2,
        Magic = 1 << 3,
        Fire = 1 << 4,
        Environmental = 1 << 5,
    }

    /// <summary>Per-hit modifiers that bypass parts of the damage pipeline.</summary>
    [Flags]
    public enum DamageFlags
    {
        None = 0,
        /// <summary>Lands even while the receiver is in an invulnerability window.</summary>
        IgnoreInvulnerability = 1 << 0,
        /// <summary>Cannot be reduced by blocking or shielding defenses.</summary>
        Unblockable = 1 << 1,
        /// <summary>Applies damage but never moves the receiver.</summary>
        NoKnockback = 1 << 2,
        /// <summary>Applies damage but never contributes to poise loss.</summary>
        NoStagger = 1 << 3,
        /// <summary>Does not start an invulnerability window on the receiver.</summary>
        NoInvulnerabilityWindow = 1 << 4,
    }

    /// <summary>
    /// What the receiver decided to do about a hit. The attacker side reads this to
    /// drive hit-stop, VFX and audio without knowing anything about the receiver.
    /// </summary>
    public enum HitReaction
    {
        None = 0,
        Flinch,
        Stagger,
        Knockback,
        Blocked,
        Deflected,
        Immune,
        Death,
    }
}
