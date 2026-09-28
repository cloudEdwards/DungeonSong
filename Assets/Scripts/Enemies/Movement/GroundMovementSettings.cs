using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Tuning for enemies that walk on floors under gravity.
    /// <para>
    /// This lives in its own file on purpose. Unity creates a MonoScript only for the type
    /// whose name matches the file name, so a ScriptableObject subclass that shares a file
    /// with another type serializes with a null script reference and silently loads as
    /// null — which shows up as an enemy that refuses to move.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "GroundMovementSettings", menuName = "Dungeon/Enemies/Movement/Ground")]
    public class GroundMovementSettings : MovementSettings
    {
        [Header("Gravity")]
        [Min(0f)] public float GravityScale = 3f;

        [Min(0f), Tooltip("Terminal falling speed.")]
        public float MaxFallSpeed = 18f;

        [Header("Jump")]
        [Min(0f), Tooltip("Default upward impulse for TryJump.")]
        public float JumpForce = 7f;

        [Header("Dash")]
        [Min(0f), Tooltip("Default dash speed for TryDash and charge attacks.")]
        public float DashForce = 10f;

        [Min(0f)] public float DashDuration = 0.25f;

        [Min(0f), Tooltip("Seconds before another dash is allowed.")]
        public float DashCooldown = 1f;
    }
}
