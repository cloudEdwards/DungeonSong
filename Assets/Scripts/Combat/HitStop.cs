using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Briefly freezes time on impact, which is most of what makes a hit feel like it
    /// connected. Shared by every attacker, so the player and a boss request it the same way.
    /// <para>
    /// Requests do not queue: the longest outstanding freeze wins, so a flurry of hits in
    /// one frame cannot stack into a visible stall.
    /// </para>
    /// </summary>
    public static class HitStop
    {
        private static HitStopRunner runner;
        private static float remaining;
        private static float restoreScale = 1f;

        /// <summary>True while a freeze is in progress.</summary>
        public static bool IsActive => remaining > 0f;

        /// <summary>
        /// Freezes time for <paramref name="seconds"/>. Ignored when a longer freeze is
        /// already running, and when the game is already paused.
        /// </summary>
        public static void Request(float seconds)
        {
            if (seconds <= 0f || !Application.isPlaying)
            {
                return;
            }

            if (remaining <= 0f)
            {
                // Remember whatever the game was running at, not an assumed 1.
                restoreScale = Time.timeScale;
                if (Mathf.Approximately(restoreScale, 0f))
                {
                    return;
                }
            }

            remaining = Mathf.Max(remaining, seconds);
            Time.timeScale = 0f;
            EnsureRunner();
        }

        /// <summary>Ends any freeze immediately and restores the previous time scale.</summary>
        public static void Cancel()
        {
            remaining = 0f;
            Time.timeScale = restoreScale;
        }

        private static void EnsureRunner()
        {
            if (runner != null)
            {
                return;
            }

            var holder = new GameObject("~HitStop") { hideFlags = HideFlags.HideAndDontSave };
            runner = holder.AddComponent<HitStopRunner>();
            Object.DontDestroyOnLoad(holder);
        }

        // Ticked in unscaled time, because scaled time is exactly what is frozen.
        internal static void Tick(float unscaledDeltaTime)
        {
            if (remaining <= 0f)
            {
                return;
            }

            remaining -= unscaledDeltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                Time.timeScale = restoreScale;
            }
        }

        private class HitStopRunner : MonoBehaviour
        {
            private void Update() => Tick(Time.unscaledDeltaTime);
        }
    }
}
