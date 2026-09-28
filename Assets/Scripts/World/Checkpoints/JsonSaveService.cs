using System;
using System.IO;
using UnityEngine;

namespace DungeonSong.World
{
    /// <summary>
    /// Minimal JSON save, written to the platform's persistent data path.
    /// <para>
    /// Deliberately small. The point of the exercise was to keep checkpointing independent
    /// of persistence, so this can be replaced wholesale later without touching campfires.
    /// </para>
    /// </summary>
    public class JsonSaveService : ISaveService
    {
        private const string FileName = "dungeonsong.save.json";

        private static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public bool HasSave => File.Exists(Path);

        public void Save(SaveData data)
        {
            if (data == null)
            {
                return;
            }

            data.SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                // A failed save must never take the game down with it.
                Debug.LogError($"Failed to write save to '{Path}': {e.Message}");
            }
        }

        public SaveData Load()
        {
            if (!HasSave)
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to read save from '{Path}': {e.Message}");
                return null;
            }
        }

        public void Delete()
        {
            if (HasSave)
            {
                File.Delete(Path);
            }
        }
    }

    /// <summary>
    /// The save service the game uses. A property rather than a singleton MonoBehaviour so
    /// tests can swap in an in-memory implementation.
    /// </summary>
    public static class GameSave
    {
        private static ISaveService service;

        public static ISaveService Service
        {
            get => service ??= new JsonSaveService();
            set => service = value;
        }
    }
}
