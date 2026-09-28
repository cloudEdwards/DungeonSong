using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Shared detection tuning. A simple enemy uses a radius and nothing else; a cautious
    /// one adds a view cone, line of sight and memory. Same component either way.
    /// </summary>
    [CreateAssetMenu(fileName = "PerceptionSettings", menuName = "Dungeon/Enemies/Perception Settings")]
    public class PerceptionSettings : ScriptableObject
    {
        [Header("Ranges")]
        [Min(0f), Tooltip("Radius within which a target can be noticed.")]
        public float DetectionRadius = 6f;

        [Min(0f), Tooltip("Radius beyond which a known target is dropped. Keep larger than detection to avoid flickering at the boundary.")]
        public float LoseRadius = 9f;

        [Header("View Cone")]
        [Range(0f, 360f), Tooltip("Width of the view cone, centred on facing. 360 sees in every direction.")]
        public float FieldOfViewDegrees = 200f;

        [Tooltip("Notice targets that touch the enemy regardless of the view cone.")]
        public bool AlwaysNoticeAtContact = true;

        [Min(0f), Tooltip("Distance treated as contact for the option above.")]
        public float ContactRadius = 1f;

        [Header("Line of Sight")]
        [Tooltip("Require an unobstructed line to the target.")]
        public bool RequireLineOfSight = true;

        [Tooltip("Layers that block sight.")]
        public LayerMask ObstructionMask = 1 << 8;

        [Header("Memory")]
        [Min(0f), Tooltip("Seconds a lost target is remembered and searched for. 0 forgets instantly.")]
        public float MemoryDuration = 3f;

        [Header("Hearing")]
        [Min(0f), Tooltip("Radius within which noise events are noticed. 0 disables hearing.")]
        public float HearingRadius;

        [Header("Performance")]
        [Min(0.02f), Tooltip("Seconds between perception evaluations. 0.1 (ten times a second) is plenty for most enemies and costs a tenth of a per-frame check.")]
        public float TickInterval = 0.1f;
    }
}
