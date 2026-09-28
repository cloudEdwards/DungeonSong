using System;

namespace DungeonSong.World
{
    /// <summary>
    /// Everything persisted between sessions. Kept a plain serializable class so the
    /// storage layer stays a detail: JSON today, something else later.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Format version, so old saves can be migrated rather than discarded.</summary>
        public int Version = 1;

        public CheckpointState Checkpoint;

        /// <summary>Health the player resumes with. -1 means "full".</summary>
        public float PlayerHealth = -1f;

        /// <summary>Unix seconds the save was written, for save-slot UI.</summary>
        public long SavedAtUnixSeconds;
    }
}
