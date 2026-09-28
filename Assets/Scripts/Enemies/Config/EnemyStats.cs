using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Shared, read-only combat numbers for a kind of enemy. Several prefabs may point at
    /// one asset so a balance pass is one edit.
    /// <para>
    /// Important: this asset holds configuration only, never live health. Runtime state
    /// lives on <see cref="EnemyHealth"/> per instance, so twenty enemies sharing this
    /// asset still have twenty separate health pools.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyStats", menuName = "Dungeon/Enemies/Enemy Stats")]
    public class EnemyStats : ScriptableObject
    {
        [Header("Health")]
        [Min(1f)] public float MaxHealth = 30f;

        [Tooltip("Seconds of invulnerability granted after taking a hit. 0 lets attacks chain freely.")]
        [Min(0f)] public float HitInvulnerability = 0.15f;

        [Header("Poise / Stagger")]
        [Tooltip("Poise damage absorbed before staggering. 0 means every hit staggers.")]
        [Min(0f)] public float MaxPoise = 20f;

        [Tooltip("Seconds without taking poise damage before poise starts refilling.")]
        [Min(0f)] public float PoiseRegenDelay = 1.5f;

        [Tooltip("Poise restored per second once regeneration starts.")]
        [Min(0f)] public float PoiseRegenRate = 10f;

        [Tooltip("Seconds spent staggered when poise breaks.")]
        [Min(0f)] public float StaggerDuration = 0.6f;

        [Header("Hit Reaction")]
        [Tooltip("Play a short flinch on hits that do not break poise.")]
        public bool FlinchOnHit = true;

        [Min(0f)] public float FlinchDuration = 0.15f;

        [Header("Knockback")]
        [Range(0f, 1f), Tooltip("Fraction of incoming knockback ignored. 1 = immovable.")]
        public float KnockbackResistance;

        [Header("Death")]
        [Tooltip("Seconds between dying and despawning, to let the death animation play.")]
        [Min(0f)] public float DespawnDelay = 1.5f;
    }
}
