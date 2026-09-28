using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// A condition that must hold before an ability can be used: standing at a campfire,
    /// being airborne, having an enemy in range, holding an item.
    /// <para>
    /// Requirements are assets, so gating an ability is a designer decision rather than a
    /// code change, and one requirement can gate many abilities. Concrete requirements may
    /// live in whichever assembly owns the knowledge they need — a "near a rest point"
    /// requirement belongs to the world, not to the player.
    /// </para>
    /// </summary>
    public abstract class AbilityRequirement : ScriptableObject
    {
        [Header("Requirement")]
        [Tooltip("Shown to the player when the requirement is not met, e.g. 'Rest at a campfire to heal'.")]
        public string UnmetMessage = "You cannot do that here.";

        /// <summary>True when this requirement currently permits activation.</summary>
        public abstract bool IsSatisfied(PlayerActor player);
    }
}
