namespace DungeonSong.Enemies
{
    /// <summary>
    /// One behaviour an enemy can be doing. States declare for themselves when they want
    /// control and whether they can be interrupted; nothing owns a table of transitions,
    /// which is what lets a new behaviour be added as a component instead of an edit to a
    /// central switch.
    /// </summary>
    public interface IEnemyState
    {
        /// <summary>Name for debug overlays.</summary>
        string StateName { get; }

        /// <summary>
        /// Higher wins. A state may pre-empt the running state only if its priority is
        /// strictly higher and the running state allows interruption.
        /// </summary>
        int Priority { get; }

        /// <summary>True when this state has something to do right now.</summary>
        bool WantsControl { get; }

        /// <summary>False when this state is unavailable (on cooldown, missing wiring).</summary>
        bool CanEnter { get; }

        /// <summary>
        /// False during committed sequences, such as the active frames of a heavy attack,
        /// so nothing can yank control away mid-swing.
        /// </summary>
        bool IsInterruptible { get; }

        /// <summary>True once the state is done and the machine should re-arbitrate.</summary>
        bool IsFinished { get; }

        void EnterState();

        void ExitState();

        void StateTick(float deltaTime);

        void StateFixedTick(float fixedDeltaTime);
    }
}
