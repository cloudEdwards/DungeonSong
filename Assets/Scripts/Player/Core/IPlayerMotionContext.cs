using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// What combat needs to know about how the player is moving, and the one lever it is
    /// allowed to pull.
    /// <para>
    /// Implemented by the existing PlayerController. This is deliberately the whole contract:
    /// attacks ask "am I airborne, on a wall, which way am I facing?" and may request a
    /// movement lock, but they never reach into movement code. That is what keeps the
    /// controller from growing a combat system inside it.
    /// </para>
    /// </summary>
    public interface IPlayerMotionContext
    {
        /// <summary>+1 facing right, -1 facing left.</summary>
        int FacingDirection { get; }

        bool IsGrounded { get; }

        bool IsTouchingWall { get; }

        bool IsWallSliding { get; }

        /// <summary>True during a roll or other committed movement that combat should respect.</summary>
        bool IsInCommittedMove { get; }

        Vector2 Velocity { get; }

        /// <summary>
        /// Suppresses the player's own horizontal input-driven movement, for attacks that
        /// root the player. Reference-counted by the caller, not the implementer.
        /// </summary>
        void SetMovementLock(bool locked);

        /// <summary>Overrides velocity, for pogo bounces, lunges and knockback.</summary>
        void SetVelocity(Vector2 velocity);

        /// <summary>Turns the player to face a direction without moving.</summary>
        void SetFacing(int sign);
    }
}
