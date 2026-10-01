using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>
    /// Checks over every enemy prefab, for wiring mistakes that compile and load fine but
    /// leave an enemy broken in play.
    /// </summary>
    public class EnemyPrefabLintTests
    {
        private static IEnumerable<TestCaseData> EnemyPrefabs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<Enemy>() != null)
                {
                    yield return new TestCaseData(path).SetName($"MeleeEnemy_CanCloseDistance({prefab.name})");
                }
            }
        }

        /// <summary>
        /// A melee attack only fires in range, and only Chase or MaintainDistance walk an
        /// enemy into range. Without one, the enemy spots the player and stands still: the
        /// GrayOozling shipped like this after MaintainDistance was removed.
        /// </summary>
        [TestCaseSource(nameof(EnemyPrefabs))]
        public void MeleeEnemy_CanCloseDistance(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponent<MeleeAttack>() == null)
            {
                Assert.Pass("No melee attack.");
            }

            bool canApproach = prefab.GetComponent<ChaseState>() != null || prefab.GetComponent<MaintainDistanceState>() != null;
            Assert.IsTrue(canApproach, $"{prefab.name} has a MeleeAttack but no ChaseState or MaintainDistanceState, so it can never reach the player.");
        }
    }
}
