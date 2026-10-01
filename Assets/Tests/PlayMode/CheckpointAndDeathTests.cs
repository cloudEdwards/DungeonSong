using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using DungeonSong.Combat;
using DungeonSong.Player;
using DungeonSong.World;

namespace DungeonSong.PlayMode.Tests
{
    /// <summary>Keeps saves in memory, so tests never touch the real save file.</summary>
    internal class InMemorySaveService : ISaveService
    {
        public SaveData Stored;

        public bool HasSave => Stored != null;

        public void Save(SaveData data) => Stored = data;

        public SaveData Load() => Stored;

        public void Delete() => Stored = null;
    }

    /// <summary>
    /// Death, respawn, campfires and save resume, played out in the real scenes. Every test
    /// here is a bug that was found and fixed by playing; these keep it fixed.
    /// </summary>
    public class CheckpointAndDeathTests
    {
        private const string SceneA = "Scene1";
        private const string SceneB = "Scene2";
        private const float Timeout = 10f;

        private ISaveService realSave;
        private bool realRunInBackground;
        private InMemorySaveService save;
        private readonly List<string> warnings = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Play mode stops advancing frames while the Editor is unfocused, which stalls
            // any test that waits on frames or physics.
            realRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            realSave = GameSave.Service;
            save = new InMemorySaveService();
            GameSave.Service = save;
            CheckpointService.Clear();
            ResourcePool.ResetSession();
            warnings.Clear();
            Application.logMessageReceived += Capture;
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= Capture;

            // Health lives in a ScriptableObject asset; leave it full rather than dead.
            PlayerActor player = UnityEngine.Object.FindAnyObjectByType<PlayerActor>();
            player?.Health?.RestoreToFull();

            var travel = GameObject.Find(nameof(CheckpointTravel));
            if (travel != null)
            {
                UnityEngine.Object.Destroy(travel);
            }

            Application.runInBackground = realRunInBackground;
            GameSave.Service = realSave;
            CheckpointService.Clear();
            ResourcePool.ResetSession();
            yield return null;
        }

        private void Capture(string message, string stack, LogType type)
        {
            if (type == LogType.Warning)
            {
                warnings.Add(message);
            }
        }

        // --- Helpers ---

        private static IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + Timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Timed out waiting for {what}.");
                }

                yield return null;
            }
        }

        private static PlayerActor Player => UnityEngine.Object.FindAnyObjectByType<PlayerActor>();

        private static Campfire Campfire => UnityEngine.Object.FindAnyObjectByType<Campfire>();

        private static Animator AnimatorOf(PlayerActor player) => player.GetComponent<Animator>();

        private static bool InState(PlayerActor player, string state)
        {
            Animator animator = AnimatorOf(player);
            return animator.GetCurrentAnimatorStateInfo(0).IsName(state)
                || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state));
        }

        private static bool Dying(PlayerActor player) => InState(player, "Death") || InState(player, "DeathNoBlood");

        private static void Kill(PlayerActor player)
        {
            DamageInfo info = DamageInfo.Create(9999f, DamageTeam.Enemy, player.transform.position);
            info.Flags |= DamageFlags.IgnoreInvulnerability;
            DamageResult result = player.GetComponent<IDamageable>().TakeDamage(in info);
            Assert.IsTrue(result.Killed, "The test kill must land.");
        }

        private static float Amount(PlayerActor player, string id)
        {
            foreach (ResourceDefinition r in player.Resources.Resources)
            {
                if (r.Id == id)
                {
                    return player.Resources.GetAmount(r);
                }
            }

            Assert.Fail($"Player carries no resource '{id}'.");
            return 0f;
        }

        // --- Death ---

        [UnityTest]
        public IEnumerator KillingHit_PlaysTheDeathAnimationImmediately()
        {
            yield return Load(SceneB);
            PlayerActor player = Player;

            Kill(player);

            // A few frames for the animator to take the trigger — not the 1.5s respawn delay.
            bool sawDeath = false;
            for (int i = 0; i < 10 && !sawDeath; i++)
            {
                yield return null;
                sawDeath = Dying(player);
            }

            Assert.IsTrue(sawDeath, "Death must play when health hits zero, not after the respawn.");
        }

        [UnityTest]
        public IEnumerator Respawn_InTheSameScene_ReturnsToIdleAtTheCampfire()
        {
            yield return Load(SceneB);
            Campfire fire = Campfire;
            fire.Rest(Player);
            Vector2 campfire = fire.RespawnPosition;

            Kill(Player);
            yield return WaitFor(() => Player != null && Player.IsAlive, "the respawn");
            yield return null;
            yield return null;

            PlayerActor player = Player;
            Assert.IsFalse(Dying(player), "A respawned player must not be left in the death pose.");
            Assert.AreEqual(campfire.x, player.transform.position.x, 0.1f);
            Assert.AreEqual(player.Health.Max, player.Health.Current, 0.001f);
            CollectionAssert.DoesNotContain(warnings, "Cannot use 'linearVelocity' on a static body.",
                "Respawn must revive the body before touching its velocity.");
        }

        [UnityTest]
        public IEnumerator Respawn_TravelsToTheCampfiresScene()
        {
            yield return Load(SceneB);
            Campfire fire = Campfire;
            fire.Rest(Player);
            Vector2 campfire = fire.RespawnPosition;

            yield return Load(SceneA);
            PlayerActor doomed = Player;
            doomed.Resources.SetAmountById("loyalty", 60f);
            Kill(doomed);

            yield return WaitFor(() => SceneManager.GetActiveScene().name == SceneB && GameObject.Find(nameof(CheckpointTravel)) == null,
                "the trip back to the campfire's scene");

            PlayerActor player = Player;
            Assert.AreEqual(campfire.x, player.transform.position.x, 0.1f);
            Assert.AreEqual(campfire.y, player.transform.position.y, 0.5f);
            Assert.IsTrue(player.IsAlive);
            Assert.AreEqual(player.Health.Max, player.Health.Current, 0.001f, "Arrives at full health.");
            Assert.AreEqual(0f, Amount(player, "loyalty"), "Dying lost the Loyalty.");
            Assert.AreEqual(2f, Amount(player, "paladin_slots"));

            // The new scene's player arrived after a death; it must not replay one.
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                Assert.IsFalse(Dying(player), $"Death replayed on arrival (frame {i}).");
            }
        }

        // --- Campfire and save ---

        [UnityTest]
        public IEnumerator LongRest_SavesCheckpointAndLoyalty()
        {
            yield return Load(SceneB);
            PlayerActor player = Player;
            player.Resources.SetAmountById("loyalty", 40f);
            player.Resources.SetAmountById("paladin_slots", 0f);

            Campfire.Rest(player);

            Assert.IsNotNull(save.Stored, "Resting at a campfire saves.");
            Assert.AreEqual(SceneB, save.Stored.Checkpoint.SceneName);
            Assert.AreEqual(Campfire.RestPointId, save.Stored.Checkpoint.RestPointId);
            Assert.AreEqual(2f, Amount(player, "paladin_slots"), "A long rest refills Paladin slots.");

            float savedLoyalty = -1f;
            foreach (ResourceSnapshot snapshot in save.Stored.Resources)
            {
                if (snapshot.Id == "loyalty")
                {
                    savedLoyalty = snapshot.Amount;
                }
            }

            Assert.AreEqual(40f, savedLoyalty, "Loyalty is written to the save untouched.");
        }

        [UnityTest]
        public IEnumerator ResumingASave_LoadsItsSceneCampfireAndLoyalty()
        {
            yield return Load(SceneB);
            Vector2 campfire = Campfire.RespawnPosition;
            string campfireId = Campfire.RestPointId;

            yield return Load(SceneA);
            var checkpoint = new CheckpointState
            {
                RestPointId = campfireId,
                SceneName = SceneB,
                X = campfire.x,
                Y = campfire.y,
                Facing = -1,
            };
            var resources = new[]
            {
                new ResourceSnapshot { Id = "loyalty", Amount = 55f },
                new ResourceSnapshot { Id = "warlock_slots", Amount = 1f },
            };

            CheckpointService.Restore(checkpoint);
            CheckpointTravel.Begin(checkpoint, resources, respawn: false);

            yield return WaitFor(() => GameObject.Find(nameof(CheckpointTravel)) == null, "the save to resume");

            PlayerActor player = Player;
            Assert.AreEqual(SceneB, SceneManager.GetActiveScene().name);
            Assert.AreEqual(campfire.x, player.transform.position.x, 0.1f);
            Assert.AreEqual(-1, player.FacingDirection);
            Assert.AreEqual(55f, Amount(player, "loyalty"), "The same Loyalty as when the game was saved.");
            Assert.AreEqual(1f, Amount(player, "warlock_slots"), "Saved values win over the respawn refill.");
        }

        // --- Loyalty ---

        [UnityTest]
        public IEnumerator LandingAHit_FillsLoyalty_ButImmuneAndEnemyHitsDoNot()
        {
            yield return Load(SceneA);
            PlayerActor player = Player;
            Hurtbox enemy = null;
            foreach (Hurtbox h in UnityEngine.Object.FindObjectsByType<Hurtbox>())
            {
                if (h.Team == DamageTeam.Enemy)
                {
                    enemy = h;
                    break;
                }
            }

            Assume.That(enemy, Is.Not.Null, "Scene1 needs an enemy for this test.");
            Assert.AreEqual(0f, Amount(player, "loyalty"), "A fresh session starts with no Loyalty.");

            enemy.Receive(DamageInfo.Create(1f, player.Team, enemy.transform.position, player.gameObject));
            Assert.AreEqual(10f, Amount(player, "loyalty"), "+10 Loyalty per landed hit.");

            // The enemy's hit invulnerability makes this one immune.
            enemy.Receive(DamageInfo.Create(1f, player.Team, enemy.transform.position, player.gameObject));
            Assert.AreEqual(10f, Amount(player, "loyalty"), "A hit that does not land earns nothing.");

            yield return new WaitForSeconds(1f);
            enemy.Receive(DamageInfo.Create(1f, player.Team, enemy.transform.position, null));
            Assert.AreEqual(10f, Amount(player, "loyalty"), "Only the player's own hits count.");
        }
    }
}
