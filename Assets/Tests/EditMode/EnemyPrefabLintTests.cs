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
        private static IEnumerable<TestCaseData> EnemyPrefabs() => Prefabs("MeleeEnemy_CanCloseDistance");

        private static IEnumerable<TestCaseData> ChasingMeleePrefabs() => Prefabs("Chase_StopsWithinAttackReach");

        private static IEnumerable<TestCaseData> Prefabs(string testName)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<Enemy>() != null)
                {
                    yield return new TestCaseData(path).SetName($"{testName}({prefab.name})");
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

        /// <summary>
        /// The chase stops at <c>stopDistance</c>; the swing reaches <c>MaxRange</c>. If the
        /// chase stops further out than the swing reaches, the enemy walks up to the player
        /// and then never attacks.
        /// </summary>
        [TestCaseSource(nameof(ChasingMeleePrefabs))]
        public void Chase_StopsWithinAttackReach(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var chase = prefab.GetComponent<ChaseState>();
            var melee = prefab.GetComponent<MeleeAttack>();
            if (chase == null || melee == null)
            {
                Assert.Pass("Does not chase into melee.");
            }

            float stop = new SerializedObject(chase).FindProperty("stopDistance").floatValue;
            var definition = new SerializedObject(melee).FindProperty("definition").objectReferenceValue as AttackDefinition;
            Assert.IsNotNull(definition, $"{prefab.name}'s MeleeAttack has no AttackDefinition.");
            Assert.Less(stop, definition.MaxRange, $"{prefab.name} stops chasing at {stop} but its {definition.name} only reaches {definition.MaxRange}.");
        }
    }
}
