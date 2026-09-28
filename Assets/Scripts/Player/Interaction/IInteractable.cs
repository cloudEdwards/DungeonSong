using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Something the player can walk up to and use: a campfire, a door, an NPC, a chest.
    /// <para>
    /// The player knows only this interface. A campfire is one implementation, which is why
    /// adding shrines or beds later needs no change to the player at all.
    /// </para>
    /// </summary>
    public interface IInteractable
    {
        Transform Transform { get; }

        /// <summary>Short verb shown in the prompt, e.g. "Rest", "Open", "Talk".</summary>
        string InteractionPrompt { get; }

        /// <summary>False while this interactable is unavailable (already used, locked).</summary>
        bool CanInteract(PlayerActor player);

        /// <summary>Performs the interaction.</summary>
        void Interact(PlayerActor player);
    }
}
