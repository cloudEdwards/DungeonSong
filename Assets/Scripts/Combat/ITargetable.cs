using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Something an AI can aim at. Deliberately not "the player": perception works
    /// against a list of these, so decoys, summons or a second player need no new code.
    /// </summary>
    public interface ITargetable
    {
        Transform Transform { get; }

        /// <summary>World point to aim at, usually centre-of-mass rather than the feet.</summary>
        Vector2 AimPosition { get; }

        DamageTeam Team { get; }

        /// <summary>False while dead, hidden or otherwise not worth pursuing.</summary>
        bool IsValidTarget { get; }
    }
}
