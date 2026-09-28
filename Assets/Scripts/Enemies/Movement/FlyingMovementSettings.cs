using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Tuning for enemies that fly, ignoring gravity.
    /// <para>
    /// Kept in its own file so Unity can bind a MonoScript to it; see
    /// <see cref="GroundMovementSettings"/> for why that matters.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "FlyingMovementSettings", menuName = "Dungeon/Enemies/Movement/Flying")]
    public class FlyingMovementSettings : MovementSettings
    {
        [Header("Steering")]
        [Min(0f), Tooltip("How sharply the flyer can change direction. Low values feel heavy and mothlike.")]
        public float SteeringResponse = 6f;

        [Header("Bobbing")]
        [Min(0f), Tooltip("Vertical wander amplitude in units. 0 disables bobbing.")]
        public float BobAmplitude = 0.25f;

        [Min(0f), Tooltip("Vertical wander frequency in cycles/second.")]
        public float BobFrequency = 1.2f;

        [Header("Altitude")]
        [Min(0f), Tooltip("Keep this distance above terrain when idling. 0 disables altitude holding.")]
        public float PreferredAltitude;
    }
}
