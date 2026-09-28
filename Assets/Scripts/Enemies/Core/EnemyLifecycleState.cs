namespace DungeonSong.Enemies
{
    /// <summary>
    /// Where an enemy is in its existence, as opposed to what its AI is currently doing
    /// (that is the state machine's business). Kept deliberately small: the expensive
    /// distinction is Active vs Dormant, which is what lets a room hold many enemies.
    /// </summary>
    public enum EnemyLifecycleState
    {
        /// <summary>Components exist but nothing has been wired yet.</summary>
        Uninitialized = 0,

        /// <summary>Wired and in the world, but not simulating. Cheap to hold in bulk.</summary>
        Dormant,

        /// <summary>Fully simulating: perception, brain, movement and attacks all tick.</summary>
        Active,

        /// <summary>Out of health. Still ticking so death behaviour can play out.</summary>
        Dead,

        /// <summary>Removed from play, either destroyed or parked in a pool.</summary>
        Despawned,
    }
}
