using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Arbitrates between the state components on an enemy.
    /// <para>
    /// There is no transition table and no switch statement. Each frame the machine asks
    /// the states with a higher priority than the running one whether they want control,
    /// and hands it over if the running state permits. Adding a behaviour to an enemy is
    /// adding a component; adding a behaviour to the game is writing one class. Neither
    /// touches this file, which is the property that has to hold at enemy one hundred.
    /// </para>
    /// </summary>
    public class EnemyStateMachine : EnemyModule
    {
        [Header("Fallback")]
        [SerializeField, Tooltip("State entered when nothing else wants control. Leave empty to use the lowest-priority state.")]
        private EnemyStateBehaviour defaultState;

        [Header("Debug")]
        [SerializeField, Tooltip("Log every state change. Useful while building an enemy, noisy afterwards.")]
        private bool logTransitions;

        private readonly List<EnemyStateBehaviour> states = new List<EnemyStateBehaviour>(8);
        private EnemyStateBehaviour current;

        /// <summary>Raised on every state change, as (previous, next). Either may be null.</summary>
        public event Action<EnemyStateBehaviour, EnemyStateBehaviour> StateChanged;

        public override int TickOrder => ModuleTickOrder.Brain;

        public EnemyStateBehaviour CurrentState => current;

        /// <summary>Name of the running state, or "None". For debug overlays.</summary>
        public string CurrentStateName => current != null ? current.StateName : "None";

        /// <summary>Every state on this enemy, highest priority first.</summary>
        public IReadOnlyList<EnemyStateBehaviour> States => states;

        /// <summary>True when a <see cref="DeadState"/> is present to own the death sequence.</summary>
        public bool HasDeathState { get; private set; }

        protected override void OnBind()
        {
            GetComponents(states);

            // Sorted once, highest priority first, so arbitration is a short scan that
            // stops at the first state that wants control.
            states.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] is DeadState)
                {
                    HasDeathState = true;
                    break;
                }
            }

            if (defaultState == null && states.Count > 0)
            {
                defaultState = states[states.Count - 1];
            }
        }

        public override void OnEnemySpawned()
        {
            if (current != null)
            {
                current.ExitState();
                current = null;
            }

            Arbitrate();
        }

        public override void OnEnemyDespawned()
        {
            if (current != null)
            {
                current.ExitState();
                current = null;
            }
        }

        public override void Tick(float deltaTime)
        {
            // Background ticks first: a state's cooldown must be current before anything
            // asks it whether it wants control this frame.
            for (int i = 0; i < states.Count; i++)
            {
                EnemyStateBehaviour state = states[i];
                if (state != current && state.enabled)
                {
                    state.BackgroundTick(deltaTime);
                }
            }

            if (current == null)
            {
                Arbitrate();
                if (current == null)
                {
                    return;
                }
            }

            current.StateTick(deltaTime);

            if (current.IsFinished)
            {
                Arbitrate();
                return;
            }

            // Pre-emption: only states that outrank the running one get a say, and only
            // when the running state is willing to be interrupted.
            if (!current.IsInterruptible)
            {
                return;
            }

            EnemyStateBehaviour challenger = FindBest(current.Priority + 1);
            if (challenger != null)
            {
                Switch(challenger);
            }
        }

        public override void FixedTick(float fixedDeltaTime) => current?.StateFixedTick(fixedDeltaTime);

        /// <summary>Picks the highest-priority state that wants control, or the fallback.</summary>
        public void Arbitrate()
        {
            EnemyStateBehaviour next = FindBest(int.MinValue) ?? defaultState;
            if (next != null)
            {
                Switch(next);
            }
        }

        /// <summary>
        /// Hands control to a specific state. Used by event-driven states (hurt, stagger,
        /// death) and by scripted boss sequences.
        /// </summary>
        /// <param name="state">State to enter.</param>
        /// <param name="force">Ignore the running state's interruption rules.</param>
        /// <returns>False when the running state refused to be interrupted.</returns>
        public bool RequestState(EnemyStateBehaviour state, bool force = false)
        {
            if (state == null || !state.enabled)
            {
                return false;
            }

            if (state == current)
            {
                return true;
            }

            if (!force && current != null && !current.IsInterruptible)
            {
                return false;
            }

            if (!force && !state.CanEnter)
            {
                return false;
            }

            Switch(state);
            return true;
        }

        /// <summary>Hands control to the state of type <typeparamref name="T"/>, if present.</summary>
        public bool RequestState<T>(bool force = false) where T : EnemyStateBehaviour
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] is T match)
                {
                    return RequestState(match, force);
                }
            }

            return false;
        }

        /// <summary>Finds a state component by type without allocating.</summary>
        public T GetState<T>() where T : EnemyStateBehaviour
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] is T match)
                {
                    return match;
                }
            }

            return null;
        }

        private EnemyStateBehaviour FindBest(int minimumPriority)
        {
            for (int i = 0; i < states.Count; i++)
            {
                EnemyStateBehaviour state = states[i];

                if (state.Priority < minimumPriority)
                {
                    // Sorted descending: everything past here is lower still.
                    return null;
                }

                if (state == current || !state.enabled)
                {
                    continue;
                }

                if (state.WantsControl && state.CanEnter)
                {
                    return state;
                }
            }

            return null;
        }

        private void Switch(EnemyStateBehaviour next)
        {
            EnemyStateBehaviour previous = current;
            previous?.ExitState();

            current = next;
            current.EnterState();

            if (logTransitions)
            {
                Debug.Log($"[{Owner.name}] {(previous != null ? previous.StateName : "None")} -> {current.StateName}", this);
            }

            StateChanged?.Invoke(previous, current);
        }
    }
}
