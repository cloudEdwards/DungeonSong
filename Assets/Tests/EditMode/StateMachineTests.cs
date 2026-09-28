using NUnit.Framework;
using UnityEngine;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>
    /// Test states with controllable answers, so arbitration can be checked without
    /// depending on the behaviour of any shipped state.
    /// </summary>
    internal class ProbeState : EnemyStateBehaviour
    {
        public int PriorityValue;
        public bool Wants;
        public bool Interruptible = true;
        public bool Enterable = true;

        public int EnterCount;
        public int ExitCount;
        public int TickCount;

        protected override int DefaultPriority => PriorityValue;

        public override bool WantsControl => Wants;

        public override bool CanEnter => Enterable;

        public override bool IsInterruptible => Interruptible;

        protected override void OnEnter() => EnterCount++;

        protected override void OnExit() => ExitCount++;

        protected override void OnStateTick(float deltaTime) => TickCount++;

        /// <summary>Exposes the protected Finish() so tests can end a state deliberately.</summary>
        public void FinishNow() => Finish();
    }

    internal class LowProbeState : ProbeState
    {
    }

    internal class HighProbeState : ProbeState
    {
    }

    public class StateMachineTests
    {
        private Enemy enemy;

        [TearDown]
        public void TearDown() => EnemyTestFixture.Destroy(enemy);

        private (LowProbeState low, HighProbeState high, EnemyStateMachine machine) CreateEnemyWithTwoStates(
            int lowPriority = 10,
            int highPriority = 50)
        {
            var go = new GameObject("TestEnemy");
            go.AddComponent<Rigidbody2D>();
            Enemy e = go.AddComponent<Enemy>();
            EnemyHealth health = go.AddComponent<EnemyHealth>();
            EnemyStateMachine machine = go.AddComponent<EnemyStateMachine>();

            LowProbeState low = go.AddComponent<LowProbeState>();
            HighProbeState high = go.AddComponent<HighProbeState>();
            low.PriorityValue = lowPriority;
            high.PriorityValue = highPriority;
            low.Wants = true;
            high.Wants = false;

            e.Initialize();
            health.Configure(EnemyTestFixture.CreateStats());
            e.Spawn();

            enemy = e;
            return (low, high, machine);
        }

        [Test]
        public void Arbitration_PicksTheOnlyStateThatWantsControl()
        {
            var (low, _, machine) = CreateEnemyWithTwoStates();

            Assert.AreSame(low, machine.CurrentState);
            Assert.AreEqual(1, low.EnterCount);
        }

        [Test]
        public void HigherPriorityState_PreemptsTheRunningState()
        {
            var (low, high, machine) = CreateEnemyWithTwoStates();

            high.Wants = true;
            machine.Tick(0.02f);

            Assert.AreSame(high, machine.CurrentState);
            Assert.AreEqual(1, low.ExitCount, "The pre-empted state must be exited.");
            Assert.AreEqual(1, high.EnterCount);
        }

        [Test]
        public void LowerPriorityState_DoesNotPreempt()
        {
            var (low, high, machine) = CreateEnemyWithTwoStates(lowPriority: 10, highPriority: 50);

            high.Wants = true;
            machine.Tick(0.02f);
            Assert.AreSame(high, machine.CurrentState);

            low.Wants = true;
            machine.Tick(0.02f);

            Assert.AreSame(high, machine.CurrentState, "A lower-priority state must never interrupt a higher one.");
        }

        [Test]
        public void NonInterruptibleState_CannotBePreempted()
        {
            var (low, high, machine) = CreateEnemyWithTwoStates();
            low.Interruptible = false;

            high.Wants = true;
            machine.Tick(0.02f);

            Assert.AreSame(low, machine.CurrentState, "A committed state must hold control even against a higher priority.");
        }

        [Test]
        public void RequestState_WithForce_OverridesNonInterruptible()
        {
            var (low, high, machine) = CreateEnemyWithTwoStates();
            low.Interruptible = false;

            bool accepted = machine.RequestState(high, force: true);

            Assert.IsTrue(accepted);
            Assert.AreSame(high, machine.CurrentState, "A forced request is how stagger and death cut through a committed attack.");
        }

        [Test]
        public void RequestState_WithoutForce_RespectsNonInterruptible()
        {
            var (low, high, machine) = CreateEnemyWithTwoStates();
            low.Interruptible = false;

            bool accepted = machine.RequestState(high);

            Assert.IsFalse(accepted);
            Assert.AreSame(low, machine.CurrentState);
        }

        [Test]
        public void StateThatCannotEnter_IsSkipped()
        {
            var (low, high, machine) = CreateEnemyWithTwoStates();
            high.Wants = true;
            high.Enterable = false;

            machine.Tick(0.02f);

            Assert.AreSame(low, machine.CurrentState);
        }

        [Test]
        public void FinishedState_TriggersReArbitration()
        {
            var (low, high, machine) = CreateEnemyWithTwoStates();
            high.Wants = true;
            machine.Tick(0.02f);
            Assert.AreSame(high, machine.CurrentState);

            high.Wants = false;
            high.FinishNow();
            machine.Tick(0.02f);

            Assert.AreSame(low, machine.CurrentState, "Finishing must hand control back to whatever still wants it.");
        }

        [Test]
        public void RunningState_ReceivesTicks()
        {
            var (low, _, machine) = CreateEnemyWithTwoStates();

            machine.Tick(0.02f);
            machine.Tick(0.02f);

            Assert.GreaterOrEqual(low.TickCount, 2);
        }

        [Test]
        public void NonRunningState_ReceivesNoStateTicks()
        {
            var (_, high, machine) = CreateEnemyWithTwoStates();

            machine.Tick(0.02f);
            machine.Tick(0.02f);

            Assert.AreEqual(0, high.TickCount, "A state that does not have control must not be ticked.");
        }

        [Test]
        public void StateChanged_EventReportsBothStates()
        {
            var (low, high, machine) = CreateEnemyWithTwoStates();
            EnemyStateBehaviour from = null;
            EnemyStateBehaviour to = null;
            machine.StateChanged += (a, b) => { from = a; to = b; };

            high.Wants = true;
            machine.Tick(0.02f);

            Assert.AreSame(low, from);
            Assert.AreSame(high, to);
        }

        [Test]
        public void GetState_FindsByType()
        {
            var (_, high, machine) = CreateEnemyWithTwoStates();

            Assert.AreSame(high, machine.GetState<HighProbeState>());
        }

    }
}
