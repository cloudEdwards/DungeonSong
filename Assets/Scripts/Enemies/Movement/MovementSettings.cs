using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Shared movement tuning. Subclassed per movement style; an
    /// <see cref="EnemyDefinition"/> holds a reference to the base type so any style fits.
    /// <para>
    /// Speeds and accelerations live here because they are balance values worth sharing.
    /// Probe geometry does not: it depends on a specific prefab's collider, so it stays on
    /// the movement component.
    /// </para>
    /// <para>
    /// Each concrete subclass lives in its own file, because Unity only binds a MonoScript
    /// to the type whose name matches the file name.
    /// </para>
    /// </summary>
    public abstract class MovementSettings : ScriptableObject
    {
        [Header("Speed")]
        [Min(0f), Tooltip("Top speed in units/second.")]
        public float MoveSpeed = 2f;

        [Min(0f), Tooltip("Units/second² while speeding up. High values feel snappy and arcade-like.")]
        public float Acceleration = 20f;

        [Min(0f), Tooltip("Units/second² while slowing down.")]
        public float Deceleration = 30f;

        [Header("Turning")]
        [Min(0f), Tooltip("Seconds to pause after turning around. Gives the player a reaction window.")]
        public float TurnDelay = 0.15f;

        [Header("Knockback")]
        [Min(0f), Tooltip("Seconds the AI stays locked out of movement after knockback.")]
        public float KnockbackControlLock = 0.2f;
    }
}
