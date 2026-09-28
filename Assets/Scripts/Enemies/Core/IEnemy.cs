using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// The surface other systems (spawners, rooms, quests, UI) use to talk to an enemy.
    /// Deliberately narrow, so gameplay code never reaches into modules.
    /// </summary>
    public interface IEnemy
    {
        Transform Transform { get; }

        DamageTeam Team { get; }

        EnemyLifecycleState Lifecycle { get; }

        bool IsAlive { get; }

        /// <summary>+1 facing right, -1 facing left.</summary>
        int FacingDirection { get; }

        ITargetable CurrentTarget { get; }

        /// <summary>Starts simulating. Safe to call when already active.</summary>
        void Activate();

        /// <summary>Stops simulating without removing the enemy from the world.</summary>
        void Deactivate();

        /// <summary>Kills the enemy through the normal death path.</summary>
        void Kill();

        /// <summary>Removes the enemy from play, returning it to its pool if pooled.</summary>
        void Despawn();
    }
}
