using UnityEditor;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.World.Editor
{
    /// <summary>Dungeon ▸ Debug: development overlays.</summary>
    public static class DebugMenu
    {
        private const string HitboxItem = "Dungeon/Debug/Show Hitboxes";

        /// <summary>
        /// Sets whether hitbox outlines start on when entering Play, and flips them live if
        /// already playing. F1 toggles them during play either way.
        /// </summary>
        [MenuItem(HitboxItem)]
        private static void ToggleHitboxes()
        {
            bool show = !EditorPrefs.GetBool(HitboxDebug.ShowOnPlayPrefKey, false);
            EditorPrefs.SetBool(HitboxDebug.ShowOnPlayPrefKey, show);

            if (EditorApplication.isPlaying)
            {
                HitboxDebug.Visible = show;
            }

            Debug.Log($"Hitbox outlines {(show ? "on" : "off")}. Toggle in play with {HitboxDebug.ToggleLabel}.");
        }

        [MenuItem(HitboxItem, true)]
        private static bool ToggleHitboxesValidate()
        {
            Menu.SetChecked(HitboxItem, EditorPrefs.GetBool(HitboxDebug.ShowOnPlayPrefKey, false));
            return true;
        }
    }
}
