using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using DungeonSong.Combat;
using DungeonSong.Player;
using DungeonSong.World;

namespace DungeonSong.PlayMode.Tests
{
    /// <summary>Enemies in the real scene, behaving as designed against the player.</summary>
    public class EnemyBehaviourTests
    {
        private ISaveService realSave;
        private bool realRunInBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            realRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            realSave = GameSave.Service;
            GameSave.Service = new InMemorySaveService();
            ResourcePool.ResetSession();
            yield return SceneManager.LoadSceneAsync("Scene1");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.FindAnyObjectByType<PlayerActor>()?.Health?.RestoreToFull();
            Application.runInBackground = realRunInBackground;
            GameSave.Service = realSave;
            ResourcePool.ResetSession();
            yield return null;
        }

        /// <summary>
        /// Keeps one enemy whose name starts with <paramref name="prefix"/> and switches every
        /// other enemy off, so nothing else interferes.
        /// </summary>
        private static Enemies.Enemy Isolate(string prefix)
        {
            Enemies.Enemy chosen = null;
            foreach (Enemies.Enemy e in Object.FindObjectsByType<Enemies.Enemy>())
            {
                if (chosen == null && e.name.StartsWith(prefix))
                {
                    chosen = e;
                    continue;
                }

                e.gameObject.SetActive(false);
            }

            return chosen;
        }

        [UnityTest]
        public IEnumerator GrayOozling_WithATarget_WalksUpAndAttacks()
        {
            Enemies.Enemy oozling = Isolate("GrayOozling");
            Assume.That(oozling, Is.Not.Null, "Scene1 needs a GrayOozling for this test.");
            PlayerActor player = Object.FindAnyObjectByType<PlayerActor>();
            player.Health.GrantInvulnerability(30f);

            // Same floor, a few steps apart, with the player as its target.
            Vector3 start = player.transform.position + new Vector3(3f, 0f, 0f);
            start.y = oozling.transform.position.y;
            oozling.transform.position = start;
            yield return new WaitForFixedUpdate();
            oozling.GetComponent<Enemies.EnemyPerception>().ForceTarget(player.GetComponent<ITargetable>());

            var brain = oozling.GetComponent<Enemies.EnemyStateMachine>();
            float startDistance = Mathf.Abs(oozling.transform.position.x - player.transform.position.x);
            bool attacked = false;
            float closest = startDistance;
            var states = new System.Text.StringBuilder();
            string last = null;

            float end = Time.time + 6f;
            while (Time.time < end && !attacked)
            {
                string state = brain.CurrentStateName;
                if (state != last)
                {
                    states.Append(state).Append(' ');
                    last = state;
                }

                closest = Mathf.Min(closest, Mathf.Abs(oozling.transform.position.x - player.transform.position.x));
                attacked = state.Contains("Attack");
                yield return new WaitForFixedUpdate();
            }

            Assert.Less(closest, startDistance - 1f, $"The oozling must walk toward the player. States: {states}");
            Assert.IsTrue(attacked, $"Once in range it attacks. States: {states}");
        }
    }
}
