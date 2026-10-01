using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DungeonSong.Player;

namespace DungeonSong.World
{
    /// <summary>
    /// Puts the player at a checkpoint that may be in another scene: loads that scene if
    /// needed, then places the player there via <see cref="PlayerActor.RespawnAt"/>.
    /// <para>
    /// Shared by resuming a save and by respawning after death. The player object is rebuilt
    /// per scene, so this lives on its own object that survives the load.
    /// </para>
    /// </summary>
    public class CheckpointTravel : MonoBehaviour
    {
        private static CheckpointTravel active;

        private CheckpointState checkpoint;
        private ResourceSnapshot[] resources;
        private bool isRespawn;

        /// <summary>True while a trip is under way.</summary>
        public static bool InProgress => active != null;

        /// <summary>
        /// Starts a trip to <paramref name="target"/>. <paramref name="savedResources"/>, when
        /// given, are written back after placement. <paramref name="respawn"/> raises
        /// <see cref="RestEvents.PlayerRespawned"/> on arrival, as a death respawn should.
        /// </summary>
        public static void Begin(CheckpointState target, ResourceSnapshot[] savedResources, bool respawn)
        {
            if (active != null)
            {
                return;
            }

            var runner = new GameObject(nameof(CheckpointTravel));
            DontDestroyOnLoad(runner);
            active = runner.AddComponent<CheckpointTravel>();
            active.checkpoint = target;
            active.resources = savedResources;
            active.isRespawn = respawn;
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        private IEnumerator Start()
        {
            string scene = checkpoint.SceneName;

            if (!string.IsNullOrEmpty(scene) && scene != SceneManager.GetActiveScene().name)
            {
                if (!Application.CanStreamedLevelBeLoaded(scene))
                {
                    Debug.LogWarning($"Checkpoint is in scene '{scene}', which is not in the build. Staying in '{SceneManager.GetActiveScene().name}'.", this);
                    Destroy(gameObject);
                    yield break;
                }

                yield return SceneManager.LoadSceneAsync(scene);
            }

            // One frame so every object in the scene has run Start, including the player's
            // own spawn, before it is moved.
            yield return null;

            PlayerActor player = FindAnyObjectByType<PlayerActor>();
            if (player == null)
            {
                Debug.LogWarning($"No player in '{SceneManager.GetActiveScene().name}' to place at the checkpoint.", this);
                Destroy(gameObject);
                yield break;
            }

            player.RespawnAt(checkpoint.Position, checkpoint.Facing);

            // After the respawn, which refills slots as a long rest would: a save wins.
            if (resources != null && player.Resources != null)
            {
                for (int i = 0; i < resources.Length; i++)
                {
                    player.Resources.SetAmountById(resources[i].Id, resources[i].Amount);
                }
            }

            // The camera lives in the predefined assembly, so it is reached by message.
            if (Camera.main != null)
            {
                Camera.main.SendMessage("SnapToCameraLookAhead", SendMessageOptions.DontRequireReceiver);
            }

            if (isRespawn)
            {
                RestEvents.RaiseRespawned(player);
            }

            Debug.Log($"Arrived at checkpoint '{checkpoint.RestPointId}' in '{SceneManager.GetActiveScene().name}'.", this);
            Destroy(gameObject);
        }
    }
}
