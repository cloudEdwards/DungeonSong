using NUnit.Framework;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>
    /// A minimal attack that records its phase callbacks, so the shared phase machine can
    /// be tested without a hitbox or a projectile in play.
    /// </summary>
    internal class ProbeAttack : AttackBehaviour
    {
        public int ActiveBeginCount;
        public int ActiveEndCount;
        public int CompleteCount;
        public int CancelCount;

        protected override void OnActiveBegin() => ActiveBeginCount++;

        protected override void OnActiveEnd() => ActiveEndCount++;

        protected override void OnAttackComplete() => CompleteCount++;

        protected override void OnAttackCancelled() => CancelCount++;
    }

    public class AttackTests
    {
        private Enemy enemy;
        private FakeTarget target;

        [TearDown]
        public void TearDown()
        {
            EnemyTestFixture.Destroy(enemy);
            target?.Dispose();
            target = null;
        }

        private static AttackDefinition CreateDefinition(
            float startup = 0.1f,
            float active = 0.1f,
            float recovery = 0.1f,
            float cooldown = 0.5f,
            float maxRange = 2f,
            bool interruptible = false)
        {
            AttackDefinition definition = ScriptableObject.CreateInstance<AttackDefinition>();
            definition.Startup = startup;
            definition.Active = active;
            definition.Recovery = recovery;
            definition.Cooldown = cooldown;
            definition.MinRange = 0f;
            definition.MaxRange = maxRange;
            definition.FacingTolerance = 180f;
            definition.RequiresGrounded = false;
            definition.RequiresLineOfSight = false;
            definition.Damage = 10f;
            definition.Interruptible = interruptible;
            return definition;
        }

        private (ProbeAttack attack, AttackController controller) CreateAttacker(AttackDefinition definition, float globalCooldown = 0f)
        {
            enemy = EnemyTestFixture.CreateEnemy(
                EnemyTestFixture.CreateStats(),
                typeof(AttackController),
                typeof(ProbeAttack));

            ProbeAttack attack = enemy.GetComponent<ProbeAttack>();
            AttackController controller = enemy.GetComponent<AttackController>();

            // The definition is a serialized private field; set it the way the inspector would.
            var field = typeof(AttackBehaviour).GetField("definition",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(attack, definition);

            var globalField = typeof(AttackController).GetField("globalCooldown",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            globalField.SetValue(controller, globalCooldown);

            return (attack, controller);
        }

        [Test]
        public void Attack_RunsThroughStartupActiveRecovery()
        {
            var (attack, _) = CreateAttacker(CreateDefinition());
            target = FakeTarget.Create(new Vector2(1f, 0f));

            attack.Begin(target);
            Assert.AreEqual(AttackPhase.Startup, attack.Phase);
            Assert.AreEqual(0, attack.ActiveBeginCount, "Nothing may be damaging during startup.");

            EnemyTestFixture.Tick(attack, 0.11f);
            Assert.AreEqual(AttackPhase.Active, attack.Phase);
            Assert.AreEqual(1, attack.ActiveBeginCount);

            EnemyTestFixture.Tick(attack, 0.11f);
            Assert.AreEqual(AttackPhase.Recovery, attack.Phase);
            Assert.AreEqual(1, attack.ActiveEndCount, "The hitbox must close when the active frames end.");

            EnemyTestFixture.Tick(attack, 0.11f);
            Assert.AreEqual(AttackPhase.Ready, attack.Phase);
            Assert.AreEqual(1, attack.CompleteCount);
        }

        [Test]
        public void Cooldown_BlocksReuseUntilItExpires()
        {
            var (attack, _) = CreateAttacker(CreateDefinition(cooldown: 0.5f));
            target = FakeTarget.Create(new Vector2(1f, 0f));

            attack.Begin(target);
            EnemyTestFixture.Tick(attack, 0.35f);

            Assert.AreEqual(AttackPhase.Ready, attack.Phase);
            Assert.IsFalse(attack.IsReady, "The attack is finished but still cooling down.");
            Assert.IsFalse(attack.CanUse(target));

            EnemyTestFixture.Tick(attack, 0.55f);
            Assert.IsTrue(attack.IsReady);
            Assert.IsTrue(attack.CanUse(target));
        }

        [Test]
        public void CanUse_RespectsRange()
        {
            var (attack, _) = CreateAttacker(CreateDefinition(maxRange: 2f));

            target = FakeTarget.Create(new Vector2(1f, 0f));
            Assert.IsTrue(attack.CanUse(target), "A target inside max range must be attackable.");

            target.transform.position = new Vector2(5f, 0f);
            Assert.IsFalse(attack.CanUse(target), "A target beyond max range must not be.");
        }

        [Test]
        public void CanUse_RespectsMinRange()
        {
            AttackDefinition definition = CreateDefinition(maxRange: 6f);
            definition.MinRange = 3f;
            var (attack, _) = CreateAttacker(definition);

            target = FakeTarget.Create(new Vector2(1f, 0f));
            Assert.IsFalse(attack.CanUse(target), "A ranged attack must refuse a target that is too close.");

            target.transform.position = new Vector2(4f, 0f);
            Assert.IsTrue(attack.CanUse(target));
        }

        [Test]
        public void CanUse_RejectsInvalidTarget()
        {
            var (attack, _) = CreateAttacker(CreateDefinition());
            target = FakeTarget.Create(new Vector2(1f, 0f));
            target.Valid = false;

            Assert.IsFalse(attack.CanUse(target));
        }

        [Test]
        public void Cancel_ClosesTheHitboxAndStartsCooldown()
        {
            var (attack, _) = CreateAttacker(CreateDefinition());
            target = FakeTarget.Create(new Vector2(1f, 0f));

            attack.Begin(target);
            EnemyTestFixture.Tick(attack, 0.11f);
            Assert.AreEqual(AttackPhase.Active, attack.Phase);

            attack.Cancel();

            Assert.AreEqual(AttackPhase.Ready, attack.Phase);
            Assert.AreEqual(1, attack.ActiveEndCount, "Cancelling mid-swing must never leave a hitbox live.");
            Assert.AreEqual(1, attack.CancelCount);
            Assert.IsFalse(attack.IsReady, "Cancelling still costs the cooldown.");
        }

        [Test]
        public void Controller_ReportsUsableAttackAndBeginsIt()
        {
            var (attack, controller) = CreateAttacker(CreateDefinition());
            target = FakeTarget.Create(new Vector2(1f, 0f));

            Assert.IsTrue(controller.HasUsableAttack(target));
            Assert.IsTrue(controller.TryBeginAttack(target));
            Assert.IsTrue(controller.IsAttacking);
            Assert.AreSame(attack, controller.CurrentAttack);
        }

        [Test]
        public void Controller_WillNotStartASecondAttackWhileOneRuns()
        {
            var (_, controller) = CreateAttacker(CreateDefinition());
            target = FakeTarget.Create(new Vector2(1f, 0f));

            controller.TryBeginAttack(target);

            Assert.IsFalse(controller.TryBeginAttack(target));
            Assert.IsFalse(controller.HasUsableAttack(target));
        }

        [Test]
        public void Controller_GlobalCooldownPacesAttacks()
        {
            var (attack, controller) = CreateAttacker(CreateDefinition(cooldown: 0f), globalCooldown: 1f);
            target = FakeTarget.Create(new Vector2(1f, 0f));

            controller.TryBeginAttack(target);
            EnemyTestFixture.Tick(attack, 0.35f);
            controller.Tick(0.35f);

            Assert.IsFalse(controller.IsAttacking);
            Assert.IsFalse(controller.HasUsableAttack(target), "The global cooldown must still be holding it back.");

            controller.Tick(1f);
            Assert.IsTrue(controller.HasUsableAttack(target));
        }

        [Test]
        public void Controller_InterruptibilityComesFromTheDefinition()
        {
            var (_, controller) = CreateAttacker(CreateDefinition(interruptible: false));
            target = FakeTarget.Create(new Vector2(1f, 0f));
            controller.TryBeginAttack(target);

            Assert.IsFalse(controller.IsCurrentAttackInterruptible);

            controller.CancelCurrentAttack();
            Assert.IsTrue(controller.IsCurrentAttackInterruptible, "With nothing running, nothing is committed.");
        }

        [Test]
        public void AnimationEvents_DriveThePhasesWhenSelected()
        {
            AttackDefinition definition = CreateDefinition();
            definition.TimingSource = AttackTimingSource.AnimationEvents;
            var (attack, controller) = CreateAttacker(definition);
            target = FakeTarget.Create(new Vector2(1f, 0f));

            controller.TryBeginAttack(target);
            Assert.AreEqual(AttackPhase.Startup, attack.Phase);

            // Without clip events the numeric timers must not advance the phase.
            EnemyTestFixture.Tick(attack, 0.15f);
            Assert.AreEqual(AttackPhase.Startup, attack.Phase, "Animation-timed attacks wait for the clip, not the clock.");

            controller.NotifyAnimationHitboxOn();
            Assert.AreEqual(AttackPhase.Active, attack.Phase);
            Assert.AreEqual(1, attack.ActiveBeginCount);

            controller.NotifyAnimationHitboxOff();
            Assert.AreEqual(AttackPhase.Recovery, attack.Phase);

            controller.NotifyAnimationAttackComplete();
            Assert.AreEqual(AttackPhase.Ready, attack.Phase);
            Assert.AreEqual(1, attack.CompleteCount);
        }
    }
}
