using System;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// A tiny global channel for "something made a noise here". Lets a smashed pot or a
    /// loud landing alert nearby enemies without anything knowing who is listening.
    /// Listeners filter by distance themselves.
    /// </summary>
    public static class NoiseEvents
    {
        /// <summary>Position, radius and the team that caused the noise.</summary>
        public static event Action<Vector2, float, DamageTeam> NoiseEmitted;

        public static void Emit(Vector2 position, float radius, DamageTeam sourceTeam)
        {
            NoiseEmitted?.Invoke(position, radius, sourceTeam);
        }
    }
}
