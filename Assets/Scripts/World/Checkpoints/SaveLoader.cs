using UnityEngine;

namespace DungeonSong.World
{
    /// <summary>
    /// Resumes the game from the last save when a play session starts: back at the campfire
    /// that wrote it, with the same Loyalty and spell slots.
    /// <para>
    /// Runs once per session, on its own, whichever scene the session starts in. The trip
    /// itself — loading the save's scene and placing the player — is
    /// <see cref="CheckpointTravel"/>, shared with respawning after death.
    /// </para>
    /// </summary>
    public static class SaveLoader
    {
#if UNITY_EDITOR
        /// <summary>EditorPrefs key for Dungeon ▸ Save ▸ Load Save On Play.</summary>
        public const string LoadOnPlayPrefKey = "DungeonSong.LoadSaveOnPlay";
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadOnStartup()
        {
#if UNITY_EDITOR
            // Lets a designer open any scene and press Play without being pulled to the campfire.
            if (!UnityEditor.EditorPrefs.GetBool(LoadOnPlayPrefKey, true))
            {
                return;
            }
#endif

            SaveData save = GameSave.Service.Load();
            if (save == null || !save.Checkpoint.IsValid)
            {
                return;
            }

            // Set before anything else, so dying before the load finishes still returns here.
            CheckpointService.Restore(save.Checkpoint);
            CheckpointTravel.Begin(save.Checkpoint, save.Resources, respawn: false);
        }
    }
}
