using UnityEngine;
using DungeonSong.Player;

namespace DungeonSong.World
{
    /// <summary>
    /// Only lets an ability run while the player is standing at a rest point.
    /// <para>
    /// This is what makes healing a thing you must return to a campfire for, rather than
    /// something you spam mid-fight. It lives in the world assembly because knowing what a
    /// campfire is, is world knowledge: the player's ability system only sees an
    /// <see cref="AbilityRequirement"/> and asks whether it is satisfied.
    /// </para>
    /// <para>
    /// The check reuses the interaction system rather than running its own physics query,
    /// so "near a campfire" means exactly the same thing as "close enough to press the
    /// interact key" — one definition of proximity, not two that can drift apart.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "RequireNearRestPoint", menuName = "Dungeon/Player/Requirements/Near Rest Point")]
    public class RequireNearRestPoint : AbilityRequirement
    {
        [Header("Rest Point")]
        [Tooltip("Require the rest point to be the player's active checkpoint, not merely nearby.")]
        public bool MustBeActiveCheckpoint;

        public override bool IsSatisfied(PlayerActor player)
        {
            if (player == null)
            {
                return false;
            }

            var interactor = player.GetModule<PlayerInteractor>();
            if (interactor == null)
            {
                return false;
            }

            // Campfires implement both IInteractable and IRestPoint, so whatever the
            // interactor has in range is the same object the player would rest at.
            if (interactor.Available is not IRestPoint restPoint)
            {
                return false;
            }

            if (MustBeActiveCheckpoint && CheckpointService.Current.RestPointId != restPoint.RestPointId)
            {
                return false;
            }

            return true;
        }
    }
}
