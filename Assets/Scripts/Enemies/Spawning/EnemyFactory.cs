using UnityEngine;
using DungeonSong.Combat.Pooling;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Creates enemies from definitions or prefabs. Everything that spawns an enemy goes
    /// through here, so pooling and initialization order are decided once.
    /// <para>
    /// Nothing in this class knows about any specific enemy type, which is the point: a
    /// spawner, a boss summon and a debug key all use the same call.
    /// </para>
    /// </summary>
    public static class EnemyFactory
    {
        /// <summary>
        /// Spawns the enemy described by <paramref name="definition"/>.
        /// </summary>
        /// <returns>The spawned enemy, or null when the definition is unusable.</returns>
        public static Enemy Spawn(EnemyDefinition definition, Vector2 position, int facing = 1, Transform parent = null)
        {
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning("EnemyFactory.Spawn called with no prefab on the definition.");
                return null;
            }

            if (definition.PrewarmCount > 0 && PrefabPool.CountInactive(definition.Prefab) == 0)
            {
                PrefabPool.Prewarm(definition.Prefab, definition.PrewarmCount);
            }

            GameObject instance = PrefabPool.Get(definition.Prefab, position, Quaternion.identity, parent);
            if (instance == null)
            {
                return null;
            }

            if (!instance.TryGetComponent(out Enemy enemy))
            {
                Debug.LogWarning($"Prefab '{definition.Prefab.name}' has no Enemy component at its root.", definition.Prefab);
                PrefabPool.Release(instance);
                return null;
            }

            enemy.Spawn(position, facing);
            return enemy;
        }

        /// <summary>Spawns a prefab directly, for cases with no definition asset.</summary>
        public static Enemy Spawn(Enemy prefab, Vector2 position, int facing = 1, Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = PrefabPool.Get(prefab.gameObject, position, Quaternion.identity, parent);
            if (instance == null || !instance.TryGetComponent(out Enemy enemy))
            {
                return null;
            }

            enemy.Spawn(position, facing);
            return enemy;
        }
    }
}
