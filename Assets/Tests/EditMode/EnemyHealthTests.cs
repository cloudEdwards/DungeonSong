using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies.Tests
{
    public class EnemyHealthTests
    {
        private Enemy enemy;

        [TearDown]
        public void TearDown() => EnemyTestFixture.Destroy(enemy);

        [Test]
        public void TakeDamage_ReducesHealth()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxHealth: 50f));

            DamageResult result = enemy.Health.TakeDamage(EnemyTestFixture.Hit(15f));

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(15f, result.AmountApplied, 0.001f);
            Assert.AreEqual(35f, enemy.Health.Current, 0.001f);
        }

        [Test]
        public void TakeDamage_AtZeroHealth_KillsAndRaisesDied()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxHealth: 10f));
            bool died = false;
            enemy.Health.Died += _ => died = true;

            DamageResult result = enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f));

            Assert.IsTrue(result.Killed);
            Assert.IsTrue(died);
            Assert.IsFalse(enemy.Health.IsAlive);
            Assert.AreEqual(0f, enemy.Health.Current);
        }

        [Test]
        public void TakeDamage_WhenDead_IsIgnored()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxHealth: 10f));
            enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f));

            DamageResult result = enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f));

            Assert.IsTrue(result.Immune);
            Assert.IsFalse(result.Applied);
        }

        [Test]
        public void TakeDamage_FromOwnTeam_IsIgnored()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats());

            DamageInfo friendlyFire = DamageInfo.Create(20f, DamageTeam.Enemy, Vector2.zero);
            DamageResult result = enemy.Health.TakeDamage(in friendlyFire);

            Assert.IsTrue(result.Immune);
            Assert.AreEqual(100f, enemy.Health.Current, 0.001f);
        }

        [Test]
        public void InvulnerabilityWindow_BlocksFollowUpHits()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(hitInvulnerability: 0.5f));

            enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f));
            DamageResult second = enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f));

            Assert.IsFalse(second.Applied, "A second hit inside the i-frame window must not land.");
            Assert.AreEqual(90f, enemy.Health.Current, 0.001f);
        }

        [Test]
        public void InvulnerabilityWindow_ExpiresAfterItsDuration()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(hitInvulnerability: 0.2f));
            enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f));

            EnemyTestFixture.Tick(enemy.Health, 0.3f);
            DamageResult second = enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f));

            Assert.IsTrue(second.Applied);
            Assert.AreEqual(80f, enemy.Health.Current, 0.001f);
        }

        [Test]
        public void IgnoreInvulnerabilityFlag_PiercesTheWindow()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(hitInvulnerability: 1f));
            enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f));

            DamageInfo piercing = EnemyTestFixture.Hit(10f);
            piercing.Flags = DamageFlags.IgnoreInvulnerability;
            DamageResult result = enemy.Health.TakeDamage(in piercing);

            Assert.IsTrue(result.Applied);
        }

        [Test]
        public void Poise_StaggersOnlyWhenItBreaks()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxPoise: 20f));

            DamageResult first = enemy.Health.TakeDamage(EnemyTestFixture.Hit(5f, poiseDamage: 8f));
            Assert.IsFalse(first.Staggered, "8 poise damage against 20 poise must not stagger.");

            DamageResult second = enemy.Health.TakeDamage(EnemyTestFixture.Hit(5f, poiseDamage: 15f));
            Assert.IsTrue(second.Staggered, "Cumulative 23 poise damage against 20 poise must stagger.");
        }

        [Test]
        public void Poise_ResetsAfterAStagger()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxPoise: 10f));

            enemy.Health.TakeDamage(EnemyTestFixture.Hit(1f, poiseDamage: 10f));
            Assert.AreEqual(10f, enemy.Health.Poise, 0.001f, "Poise should refill after breaking.");
        }

        [Test]
        public void ZeroMaxPoise_MeansEveryHitStaggers()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxPoise: 0f));

            DamageResult result = enemy.Health.TakeDamage(EnemyTestFixture.Hit(1f, poiseDamage: 1f));

            Assert.IsTrue(result.Staggered);
        }

        [Test]
        public void NoStaggerFlag_SuppressesStagger()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxPoise: 0f));

            DamageInfo info = EnemyTestFixture.Hit(1f);
            info.Flags = DamageFlags.NoStagger;
            DamageResult result = enemy.Health.TakeDamage(in info);

            Assert.IsFalse(result.Staggered);
        }

        [Test]
        public void Heal_IsClampedToMax()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxHealth: 50f));
            enemy.Health.TakeDamage(EnemyTestFixture.Hit(20f));

            enemy.Health.Heal(999f);

            Assert.AreEqual(50f, enemy.Health.Current, 0.001f);
        }

        [Test]
        public void ResetForLife_RestoresFullHealth_SoPoolReuseIsClean()
        {
            enemy = EnemyTestFixture.CreateEnemy(EnemyTestFixture.CreateStats(maxHealth: 40f));
            enemy.Health.TakeDamage(EnemyTestFixture.Hit(39f));

            enemy.Health.ResetForLife();

            Assert.AreEqual(40f, enemy.Health.Current, 0.001f);
            Assert.IsTrue(enemy.Health.IsAlive);
        }

        [Test]
        public void SharedStatsAsset_DoesNotShareRuntimeHealth()
        {
            // The existing project keeps live player health inside a ScriptableObject.
            // Enemies must not: two enemies sharing one stats asset need separate pools.
            EnemyStats shared = EnemyTestFixture.CreateStats(maxHealth: 30f);
            enemy = EnemyTestFixture.CreateEnemy(shared);
            Enemy second = EnemyTestFixture.CreateEnemy(shared);

            try
            {
                enemy.Health.TakeDamage(EnemyTestFixture.Hit(25f));

                Assert.AreEqual(5f, enemy.Health.Current, 0.001f);
                Assert.AreEqual(30f, second.Health.Current, 0.001f, "Damaging one enemy must not touch another that shares its stats asset.");
            }
            finally
            {
                EnemyTestFixture.Destroy(second);
            }
        }
    }
}
