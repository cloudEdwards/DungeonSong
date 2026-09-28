using System;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// What an enemy knows about its quarry. States query this; they never run their own
    /// distance checks or raycasts, so detection tuning stays in one place and the cost of
    /// perceiving stays bounded.
    /// </summary>
    public interface IPerception
    {
        ITargetable CurrentTarget { get; }

        bool HasTarget { get; }

        /// <summary>True when the target is currently visible, not merely remembered.</summary>
        bool HasLineOfSight { get; }

        /// <summary>Where the target was last actually seen. Valid after losing it.</summary>
        Vector2 LastKnownPosition { get; }

        /// <summary>Seconds since the target was last seen. 0 while visible.</summary>
        float TimeSinceSeen { get; }

        /// <summary>True while the target is remembered but not visible: search behaviour.</summary>
        bool IsSearching { get; }

        /// <summary>Forces a target, bypassing detection. Used by aggro triggers and bosses.</summary>
        void ForceTarget(ITargetable target);

        /// <summary>Drops the current target and any memory of it.</summary>
        void Clear();

        /// <summary>Points the enemy at a position it did not see, e.g. a noise.</summary>
        void Alert(Vector2 position);

        event Action<ITargetable> TargetAcquired;

        event Action<ITargetable> TargetLost;
    }
}
