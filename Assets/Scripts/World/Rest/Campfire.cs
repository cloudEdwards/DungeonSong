using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonSong.Player;

namespace DungeonSong.World
{
    /// <summary>
    /// A campfire: the game's long rest and checkpoint, in the spirit of a Hollow Knight
    /// bench or a D&amp;D long rest. Full health, every spell slot back, and a save.
    /// <para>
    /// It is one implementation of <see cref="IRestPoint"/> and <see cref="IInteractable"/>,
    /// and it owns none of the systems it touches. It restores the player, hands the
    /// checkpoint to <see cref="CheckpointService"/>, asks <see cref="GameSave"/> to persist,
    /// and announces the rest on <see cref="RestEvents"/>. Enemy respawning and world resets
    /// listen for that announcement; this class never learns they exist.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Campfire : MonoBehaviour, IInteractable, IRestPoint
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Stable id stored in the save. Must be unique and must not change once players have saves.")]
        private string restPointId = "campfire_01";

        [Header("Respawn")]
        [SerializeField, Tooltip("Where the player stands after respawning here. Leave empty to use this transform.")]
        private Transform respawnAnchor;

        [SerializeField, Tooltip("Direction the player faces after respawning. +1 right, -1 left.")]
        private int respawnFacing = 1;

        [Header("Rest")]
        [SerializeField, Tooltip("Restore the player to full health on rest.")]
        private bool healToFull = true;

        [SerializeField, Tooltip("Refill every resource pool that recovers on a long rest (all spell slots). Loyalty is untouched.")]
        private bool restoreResources = true;

        [SerializeField, Tooltip("Refill limited-charge abilities and tools on rest.")]
        private bool restoreAbilityCharges = true;

        [SerializeField, Tooltip("Write a save when the player rests.")]
        private bool saveOnRest = true;

        [Header("Presentation")]
        [SerializeField] private string interactionPrompt = "Rest";

        [SerializeField, Tooltip("Semantic animation key played on the player when resting.")]
        private string restAnimationKey = "rest";

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private PlayerInteractor trackedInteractor;

        public Transform Transform => transform;

        public string InteractionPrompt => interactionPrompt;

        public string RestPointId => restPointId;

        public Vector2 RespawnPosition => respawnAnchor != null ? respawnAnchor.position : transform.position;

        public int RespawnFacing => respawnFacing == 0 ? 1 : respawnFacing;

        /// <summary>True once this campfire is the active checkpoint.</summary>
        public bool IsActiveCheckpoint => CheckpointService.Current.RestPointId == restPointId;

        public bool CanInteract(PlayerActor player) => player != null && player.IsAlive;

        public void Interact(PlayerActor player) => Rest(player);

        /// <summary>
        /// Performs the rest. Public and separate from <see cref="Interact"/> so a scripted
        /// sequence can rest the player without simulating a button press.
        /// </summary>
        public void Rest(PlayerActor player)
        {
            if (player == null)
            {
                return;
            }

            player.Animation.PlayAction(restAnimationKey);

            if (healToFull)
            {
                player.Health?.RestoreToFull();
            }

            if (restoreResources)
            {
                player.Resources?.RestoreFor(RestType.Long);
            }

            if (restoreAbilityCharges)
            {
                player.Abilities?.RestoreAllCharges();
            }

            CheckpointService.SetCheckpoint(this, SceneManager.GetActiveScene().name);

            if (saveOnRest)
            {
                GameSave.Service.Save(new SaveData
                {
                    Checkpoint = CheckpointService.Current,
                    PlayerHealth = player.Health?.Current ?? -1f,
                    Resources = SnapshotResources(player.Resources),
                });
            }

            // Everything else the world wants to do about a rest happens here, from the
            // other side of an event.
            RestEvents.RaiseRested(player, this);
        }

        private static ResourceSnapshot[] SnapshotResources(ResourcePool pool)
        {
            if (pool == null)
            {
                return System.Array.Empty<ResourceSnapshot>();
            }

            var snapshots = new System.Collections.Generic.List<ResourceSnapshot>(pool.Resources.Count);
            for (int i = 0; i < pool.Resources.Count; i++)
            {
                ResourceDefinition definition = pool.Resources[i];
                if (definition != null)
                {
                    snapshots.Add(new ResourceSnapshot { Id = definition.Id, Amount = pool.GetAmount(definition) });
                }
            }

            return snapshots.ToArray();
        }

        // Registration is trigger-driven, so the player never searches the scene.
        private void OnTriggerEnter2D(Collider2D other)
        {
            var interactor = other.GetComponentInParent<PlayerInteractor>();
            if (interactor == null)
            {
                return;
            }

            trackedInteractor = interactor;
            interactor.Register(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var interactor = other.GetComponentInParent<PlayerInteractor>();
            if (interactor != null)
            {
                interactor.Unregister(this);

                if (ReferenceEquals(interactor, trackedInteractor))
                {
                    trackedInteractor = null;
                }
            }
        }

        private void OnDisable()
        {
            // Never leave a destroyed campfire in the player's candidate list.
            trackedInteractor?.Unregister(this);
            trackedInteractor = null;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = IsActiveCheckpoint ? new Color(1f, 0.75f, 0.2f, 0.9f) : new Color(1f, 0.5f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(RespawnPosition, 0.35f);
            Gizmos.DrawLine(transform.position, RespawnPosition);
        }
    }
}
