using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// A named enemy: which prefab to spawn and which shared configuration it uses.
    /// Spawners reference this rather than a prefab directly, so an encounter can be
    /// re-pointed or rebalanced without touching a scene.
    /// <para>
    /// Note what is deliberately absent: attacks. Attack components live on the prefab and
    /// each references its own <see cref="AttackDefinition"/>. Listing them here too would
    /// create a second source of truth that silently drifts.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Dungeon/Enemies/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Enemy";

        [Tooltip("Prefab with an Enemy component at its root.")]
        public GameObject Prefab;

        [Header("Shared Configuration")]
        [Tooltip("Combat numbers. The Enemy falls back to this when its EnemyHealth has no override.")]
        public EnemyStats Stats;

        [Tooltip("Movement tuning. The movement component falls back to this when it has no override.")]
        public MovementSettings Movement;

        [Tooltip("Detection tuning. The perception component falls back to this when it has no override.")]
        public PerceptionSettings Perception;

        [Header("Pooling")]
        [Tooltip("Instances to create up front when this definition is first spawned.")]
        [Min(0)] public int PrewarmCount;

        [Header("Notes")]
        [TextArea(2, 6), Tooltip("Designer notes. Not used at runtime.")]
        public string Notes;
    }
}
