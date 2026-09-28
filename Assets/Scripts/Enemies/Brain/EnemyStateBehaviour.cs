using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Base class for states. Subclasses override <see cref="WantsControl"/> to say when
    /// they apply and <see cref="OnStateTick"/> to do the work; everything else has a
    /// sensible default.
    /// <para>
    /// States are components so a designer can see, reorder and retune an enemy's whole
    /// behaviour set in the inspector without opening a script.
    /// </para>
    /// </summary>
    public abstract class EnemyStateBehaviour : EnemyModule, IEnemyState
    {
        [Header("Arbitration")]
        [SerializeField, Tooltip("Overrides this state's default priority. -1 keeps the default. Higher priorities pre-empt lower ones.")]
        private int priorityOverride = -1;

        private bool finished;

        /// <summary>Priority used when <c>priorityOverride</c> is left at -1.</summary>
        protected virtual int DefaultPriority => 0;

        public virtual string StateName => GetType().Name;

        public int Priority => priorityOverride >= 0 ? priorityOverride : DefaultPriority;

        public virtual bool WantsControl => false;

        public virtual bool CanEnter => true;

        public virtual bool IsInterruptible => true;

        public bool IsFinished => finished;

        /// <summary>True while this state is the one running.</summary>
        public bool IsCurrent { get; private set; }

        /// <summary>Seconds since this state was entered.</summary>
        public float TimeInState { get; private set; }

        // --- Convenience accessors, so subclasses read as behaviour rather than plumbing. ---

        protected IMovementController Movement => Owner.Movement;

        protected IPerception Perception => Owner.Perception;

        protected AttackController Attacks => Owner.Attacks;

        protected EnemyHealth Health => Owner.Health;

        protected IAnimatorAdapter Anim => Owner.Animation;

        protected EnemyStateMachine Machine => Owner.Brain;

        protected ITargetable Target => Owner.CurrentTarget;

        /// <summary>Horizontal distance to the current target, or infinity without one.</summary>
        protected float DistanceToTarget
        {
            get
            {
                ITargetable target = Target;
                return target != null
                    ? Vector2.Distance(transform.position, target.AimPosition)
                    : float.PositiveInfinity;
            }
        }

        // The machine drives states; Unity's tick must not reach them twice.
        public sealed override void Tick(float deltaTime) { }

        public sealed override void FixedTick(float fixedDeltaTime) { }

        public void EnterState()
        {
            IsCurrent = true;
            finished = false;
            TimeInState = 0f;
            OnEnter();
        }

        public void ExitState()
        {
            IsCurrent = false;
            OnExit();
        }

        public void StateTick(float deltaTime)
        {
            TimeInState += deltaTime;
            OnStateTick(deltaTime);
        }

        public void StateFixedTick(float fixedDeltaTime) => OnStateFixedTick(fixedDeltaTime);

        /// <summary>
        /// Driven by the machine for every state that is NOT currently running. Use it for
        /// cooldowns and pending-trigger bookkeeping that must keep running in the
        /// background, since a state receives no ticks while another state has control.
        /// </summary>
        internal void BackgroundTick(float deltaTime) => OnBackgroundTick(deltaTime);

        /// <summary>Marks the state complete so the machine picks something else.</summary>
        protected void Finish() => finished = true;

        protected virtual void OnEnter() { }

        protected virtual void OnExit() { }

        protected virtual void OnStateTick(float deltaTime) { }

        protected virtual void OnStateFixedTick(float fixedDeltaTime) { }

        /// <summary>Runs while some other state has control. See <see cref="BackgroundTick"/>.</summary>
        protected virtual void OnBackgroundTick(float deltaTime) { }

        /// <summary>Asks the machine to run this state now. Respects interruption rules unless forced.</summary>
        protected bool RequestSelf(bool force = false) => Machine != null && Machine.RequestState(this, force);
    }
}
