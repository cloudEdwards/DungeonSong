using System.IO;
using UnityEditor;
using UnityEngine;

namespace DungeonSong.World.Editor
{
    /// <summary>Dungeon ▸ Save: control whether Play resumes the save, and clear it.</summary>
    public static class SaveMenu
    {
        private const string ToggleItem = "Dungeon/Save/Load Save On Play";

        [MenuItem(ToggleItem)]
        private static void ToggleLoadOnPlay()
        {
            bool enabled = !EditorPrefs.GetBool(SaveLoader.LoadOnPlayPrefKey, true);
            EditorPrefs.SetBool(SaveLoader.LoadOnPlayPrefKey, enabled);
            Debug.Log(enabled ? "Play will resume from the save." : "Play will start in the open scene, ignoring the save.");
        }

        [MenuItem(ToggleItem, true)]
        private static bool ToggleLoadOnPlayValidate()
        {
            Menu.SetChecked(ToggleItem, EditorPrefs.GetBool(SaveLoader.LoadOnPlayPrefKey, true));
            return true;
        }

        [MenuItem("Dungeon/Save/Delete Save")]
        private static void DeleteSave()
        {
            if (!GameSave.Service.HasSave)
            {
                Debug.Log("There is no save to delete.");
                return;
            }

            if (EditorUtility.DisplayDialog("Delete save", "Delete the save file? The next Play starts fresh.", "Delete", "Cancel"))
            {
                GameSave.Service.Delete();
                Debug.Log("Save deleted.");
            }
        }

        [MenuItem("Dungeon/Save/Reveal Save File")]
        private static void RevealSave()
        {
            EditorUtility.RevealInFinder(Path.Combine(Application.persistentDataPath, JsonSaveService.FileName));
        }
    }
}
