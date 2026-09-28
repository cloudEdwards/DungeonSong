using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies.Tests
{
    public class DefenseTests
    {
        private Enemy enemy;

        [TearDown]
        public void TearDown() => EnemyTestFixture.Destroy(enemy);

        /// <summary>
        /// Builds an enemy that already has a DefenseController and the requested defense,
        /// so everything is wired by the normal Initialize path rather than bolted on after.
        /// The defense starts inactive, exactly as it would in game.
        /// </summary>
        private T CreateWithDefense<T>(EnemyStats stats = null) where T : DefenseBehaviour
        {
            enemy = EnemyTestFixture.CreateEnemy(
                stats ?? EnemyTestFixture.CreateStats(),
                typeof(DefenseController),
                typeof(T));

            return enemy.GetComponent<T>();
        }

        [Test]
        public void ArmorDefense_ReducesIncomingDamage()
        {
            ArmorDefense armor = CreateWithDefense<ArmorDefense>();
            armor.Activate();

            DamageResult result = enemy.Health.TakeDamage(EnemyTestFixture.Hit(20f));

            Assert.Less(result.AmountApplied, 20f, "Armour must reduce the hit.");
            Assert.Greater(result.AmountApplied, 0f, "Armour at default settings must not fully absorb it.");
        }

        [Test]
        public void InactiveDefense_DoesNothing()
        {
            ArmorDefense armor = CreateWithDefense<ArmorDefense>();
            armor.Deactivate();

            DamageResult result = enemy.Health.TakeDamage(EnemyTestFixture.Hit(20f));

            Assert.AreEqual(20f, result.AmountApplied, 0.001f);
        }

        [Test]
        public void InvulnerabilityDefense_RejectsDamageEntirely()
        {
            InvulnerabilityDefense invulnerable = CreateWithDefense<InvulnerabilityDefense>();
            invulnerable.Activate();

            DamageResult result = enemy.Health.TakeDamage(EnemyTestFixture.Hit(50f));

            Assert.IsTrue(result.Immune);
            Assert.IsFalse(result.Applied);
            Assert.AreEqual(100f, enemy.Health.Current, 0.001f);
        }

        [Test]
        public void InvulnerabilityDefense_ExpiresOnItsTimer()
        {
            InvulnerabilityDefense invulnerable = CreateWithDefense<InvulnerabilityDefense>();
            invulnerable.Activate(0.2f);

            EnemyTestFixture.Tick(invulnerable, 0.3f);

            Assert.IsFalse(invulnerable.IsActive);
            Assert.IsTrue(enemy.Health.TakeDamage(EnemyTestFixture.Hit(10f)).Applied);
        }

        [Test]
        public void SpikeDefense_ReflectsDamageToTheAttacker()
        {
            SpikeDefense spikes = CreateWithDefense<SpikeDefense>();
            spikes.Activate();

            DamageInfo info = EnemyTestFixture.Hit(10f);
            DamageResult result = enemy.Health.TakeDamage(in info);

            Assert.IsTrue(result.Immune, "A closed shell should reject the hit at default settings.");
            Assert.Greater(result.ReflectedDamage, 0f, "A spiky shell must push damage back.");
        }

        [Test]
        public void ShieldDefense_BlocksFromTheFrontOnly()
        {
            ShieldDefense shield = CreateWithDefense<ShieldDefense>();
            shield.Activate();
            enemy.SetFacing(1);

            DamageInfo fromFront = DamageInfo.Create(20f, DamageTeam.Player, new Vector2(5f, 0f));
            fromFront.PoiseDamage = 20f;
            DamageResult blocked = enemy.Health.TakeDamage(in fromFront);

            Assert.IsTrue(blocked.Blocked, "A hit from in front of a raised shield must be blocked.");

            enemy.Health.ResetForLife();

            DamageInfo fromBehind = DamageInfo.Create(20f, DamageTeam.Player, new Vector2(-5f, 0f));
            fromBehind.PoiseDamage = 20f;
            DamageResult throughBack = enemy.Health.TakeDamage(in fromBehind);

            Assert.IsFalse(throughBack.Blocked, "A hit from behind must bypass a frontal shield.");
            Assert.IsTrue(throughBack.Applied);
        }

        [Test]
        public void UnblockableFlag_BeatsAShield()
        {
            ShieldDefense shield = CreateWithDefense<ShieldDefense>();
            shield.Activate();
            enemy.SetFacing(1);

            DamageInfo info = DamageInfo.Create(20f, DamageTeam.Player, new Vector2(5f, 0f));
            info.Flags = DamageFlags.Unblockable;
            DamageResult result = enemy.Health.TakeDamage(in info);

            Assert.IsFalse(result.Blocked);
            Assert.IsTrue(result.Applied);
        }

        [Test]
        public void DefensesStack_MultiplicativelyThroughOnePipeline()
        {
            enemy = EnemyTestFixture.CreateEnemy(
                EnemyTestFixture.CreateStats(),
                typeof(DefenseController),
                typeof(ArmorDefense),
                typeof(InvulnerabilityDefense));

            ArmorDefense armor = enemy.GetComponent<ArmorDefense>();
            InvulnerabilityDefense invulnerable = enemy.GetComponent<InvulnerabilityDefense>();
            armor.Activate();

            DamageResult armourOnly = enemy.Health.TakeDamage(EnemyTestFixture.Hit(20f));
            enemy.Health.ResetForLife();

            invulnerable.Activate();
            DamageResult both = enemy.Health.TakeDamage(EnemyTestFixture.Hit(20f));

            Assert.Greater(armourOnly.AmountApplied, 0f);
            Assert.IsTrue(both.Immune, "Immunity from a later modifier must win over armour's multiplier.");
        }
    }
}
