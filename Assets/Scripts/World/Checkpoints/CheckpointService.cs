using System;
using UnityEngine;

namespace DungeonSong.World
{
    /// <summary>Where the player will reappear after dying.</summary>
    [Serializable]
    public struct CheckpointState
    {
        /// <summary>Id of the rest point this checkpoint came from.</summary>
        public string RestPointId;

        /// <summary>Scene the rest point lives in.</summary>
        public string SceneName;

        public float X;

        public float Y;

        public int Facing;

        public bool IsValid => !string.IsNullOrEmpty(RestPointId);

        public Vector2 Position => new Vector2(X, Y);
    }

    /// <summary>
    /// Holds the active checkpoint, and nothing else.
    /// <para>
    /// Deliberately separate from saving: this knows <em>where</em> the player respawns,
    /// while <see cref="ISaveService"/> knows how to write that to disk. Keeping them apart
    /// means the save format can change without touching checkpoints, and a checkpoint can
    /// be set without forcing a save.
    /// </para>
    /// </summary>
    public static class CheckpointService
    {
        /// <summary>Raised whenever the active checkpoint changes.</summary>
        public static event Action<CheckpointState> CheckpointChanged;

        /// <summary>The active checkpoint. Invalid until one is set or loaded.</summary>
        public static CheckpointState Current { get; private set; }

        public static bool HasCheckpoint => Current.IsValid;

        /// <summary>Makes a rest point the active checkpoint.</summary>
        public static void SetCheckpoint(IRestPoint restPoint, string sceneName)
        {
            if (restPoint == null)
            {
                return;
            }

            Vector2 position = restPoint.RespawnPosition;

            Current = new CheckpointState
            {
                RestPointId = restPoint.RestPointId,
                SceneName = sceneName,
                X = position.x,
                Y = position.y,
                Facing = restPoint.RespawnFacing == 0 ? 1 : restPoint.RespawnFacing,
            };

            CheckpointChanged?.Invoke(Current);
        }

        /// <summary>Restores a checkpoint read from a save.</summary>
        public static void Restore(CheckpointState state)
        {
            Current = state;
            CheckpointChanged?.Invoke(Current);
        }

        /// <summary>Forgets the checkpoint. For new games and tests.</summary>
        public static void Clear()
        {
            Current = default;
            CheckpointChanged?.Invoke(Current);
        }
    }
}
