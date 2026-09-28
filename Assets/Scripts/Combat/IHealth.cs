using System;
using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// A health pool other systems can read and react to, without knowing whose it is.
    /// <para>
    /// <see cref="IDamageable"/> is the write side (deal damage to this thing);
    /// this is the read-and-observe side, used by UI, death handling, rest points and
    /// healing effects. One component normally implements both.
    /// </para>
    /// </summary>
    public interface IHealth
    {
        float Current { get; }

        float Max { get; }

        /// <summary>Health as 0..1, for bars and thresholds.</summary>
        float Normalized { get; }

        bool IsAlive { get; }

        /// <summary>True while a hit cannot land, for any reason.</summary>
        bool IsInvulnerable { get; }

        /// <summary>Raised for every hit that reached this pool, including blocked ones.</summary>
        event Action<DamageInfo, DamageResult> Damaged;

        /// <summary>Raised on any change to current health, as (current, max).</summary>
        event Action<float, float> HealthChanged;

        /// <summary>Raised once when health reaches zero.</summary>
        event Action Died;

        /// <summary>Restores health, clamped to <see cref="Max"/>. Returns the amount actually restored.</summary>
        float Heal(float amount);

        /// <summary>Opens an invulnerability window, for dodges, abilities and scripted sequences.</summary>
        void GrantInvulnerability(float seconds);

        /// <summary>Refills to full and clears death state. Used on respawn and at rest points.</summary>
        void RestoreToFull();
    }
}
