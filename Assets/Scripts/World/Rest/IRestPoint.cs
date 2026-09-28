using UnityEngine;
using DungeonSong.Player;

namespace DungeonSong.World
{
    /// <summary>
    /// Somewhere the player can recover and set their respawn point: a campfire, a shrine,
    /// a safe room.
    /// <para>
    /// Separated from <see cref="DungeonSong.Player.IInteractable"/> because resting is not
    /// necessarily triggered by interaction — a scripted sequence or a story beat could
    /// also cause a rest.
    /// </para>
    /// </summary>
    public interface IRestPoint
    {
        /// <summary>Stable id used to identify this point in a save.</summary>
        string RestPointId { get; }

        /// <summary>Where the player stands after respawning here.</summary>
        Vector2 RespawnPosition { get; }

        /// <summary>Which way the player faces after respawning here.</summary>
        int RespawnFacing { get; }

        /// <summary>Performs the rest: restore, checkpoint, save, notify.</summary>
        void Rest(PlayerActor player);
    }
}
