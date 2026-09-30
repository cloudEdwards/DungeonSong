using System;

namespace DungeonSong.World
{
    /// <summary>One resource pool's value, keyed by <c>ResourceDefinition.Id</c>.</summary>
    [Serializable]
    public struct ResourceSnapshot
    {
        public string Id;

        public float Amount;
    }

    /// <summary>
    /// Everything persisted between sessions. Kept a plain serializable class so the
    /// storage layer stays a detail: JSON today, something else later.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Format version, so old saves can be migrated rather than discarded.</summary>
        public int Version = 2;

        public CheckpointState Checkpoint;

        /// <summary>Health the player resumes with. -1 means "full".</summary>
        public float PlayerHealth = -1f;

        /// <summary>Every resource pool: Loyalty and spell slots. Added in version 2.</summary>
        public ResourceSnapshot[] Resources = Array.Empty<ResourceSnapshot>();

        /// <summary>Unix seconds the save was written, for save-slot UI.</summary>
        public long SavedAtUnixSeconds;
    }
}
