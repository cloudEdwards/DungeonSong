using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonSong.Player;

namespace DungeonSong.World
{
    /// <summary>
    /// Sends the player back to their checkpoint when they die.
    /// <para>
    /// This lives on the player but in the world assembly, because it is checkpoint logic
    /// rather than player logic. The player's death code raises an event and knows nothing
    /// about campfires; this listens and asks <see cref="CheckpointService"/> where to go.
    /// That is what keeps death and checkpoints independent.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerActor))]
    public class PlayerRespawner : MonoBehaviour
    {
        [Header("Respawn")]
        [SerializeField, Min(0f), Tooltip("Seconds between dying and respawning, to let the death animation play.")]
        private float respawnDelay = 1.5f;

        [SerializeField, Tooltip("Respawn where the player started when no checkpoint has been set yet.")]
        private bool fallBackToStartPosition = true;

        private PlayerActor player;
        private Vector2 startPosition;
        private int startFacing = 1;
        private float timer;
        private bool pending;

        private void Awake()
        {
            player = GetComponent<PlayerActor>();
            startPosition = transform.position;
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.Died += OnPlayerDied;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.Died -= OnPlayerDied;
            }
        }

        private void Update()
        {
            if (!pending)
            {
                return;
            }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                pending = false;
                Respawn();
            }
        }

        private void OnPlayerDied(PlayerActor actor)
        {
            pending = true;
            timer = respawnDelay;
        }

        /// <summary>
        /// Puts the player back at the active checkpoint. Public so a menu or a scripted
        /// sequence can force a respawn.
        /// </summary>
        public void Respawn()
        {
            Vector2 position = startPosition;
            int facing = startFacing;

            if (CheckpointService.HasCheckpoint)
            {
                CheckpointState checkpoint = CheckpointService.Current;

                // Rested in another scene: go there. This player belongs to the scene being
                // left, so the new scene's player is the one placed at the campfire.
                if (!string.IsNullOrEmpty(checkpoint.SceneName) && checkpoint.SceneName != SceneManager.GetActiveScene().name)
                {
                    CheckpointTravel.Begin(checkpoint, null, respawn: true);
                    return;
                }

                position = checkpoint.Position;
                facing = checkpoint.Facing;
            }
            else if (!fallBackToStartPosition)
            {
                Debug.LogWarning("PlayerRespawner has no checkpoint and no fallback; the player stays where it died.", this);
                return;
            }

            player.RespawnAt(position, facing);
            RestEvents.RaiseRespawned(player);
        }
    }
}
