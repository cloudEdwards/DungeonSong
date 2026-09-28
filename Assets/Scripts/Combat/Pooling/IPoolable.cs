namespace DungeonSong.Combat.Pooling
{
    /// <summary>
    /// Implemented by pooled prefabs that need to reset state between uses. Pooling is
    /// otherwise transparent, so components that hold no state can ignore this.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Called after the instance is taken from the pool and activated.</summary>
        void OnSpawnedFromPool();

        /// <summary>Called before the instance is deactivated and returned to the pool.</summary>
        void OnReturnedToPool();
    }
}
