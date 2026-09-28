using System;
using DungeonSong.Player;

namespace DungeonSong.World
{
    /// <summary>
    /// A global channel announcing that the player rested.
    /// <para>
    /// This is how enemy respawning, world state resets and quest ticks hook into resting
    /// without the campfire ever knowing they exist. A campfire's job ends at "I rested";
    /// deciding what that means for the world is everyone else's.
    /// </para>
    /// </summary>
    public static class RestEvents
    {
        /// <summary>Raised after a rest completes, with the player and the point rested at.</summary>
        public static event Action<PlayerActor, IRestPoint> PlayerRested;

        /// <summary>Raised when the player respawns after death.</summary>
        public static event Action<PlayerActor> PlayerRespawned;

        public static void RaiseRested(PlayerActor player, IRestPoint restPoint) => PlayerRested?.Invoke(player, restPoint);

        public static void RaiseRespawned(PlayerActor player) => PlayerRespawned?.Invoke(player);
    }
}
