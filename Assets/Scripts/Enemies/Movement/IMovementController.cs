using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// What the rest of the enemy asks of movement. States express intent ("go that way",
    /// "stop", "jump") and the implementation decides what that means for a walker, a
    /// flyer or a wall crawler. No caller ever touches Rigidbody2D directly.
    /// </summary>
    public interface IMovementController
    {
        Vector2 Velocity { get; }

        /// <summary>Top speed for the current settings, used to normalize animation blends.</summary>
        float MaxSpeed { get; }

        bool IsGrounded { get; }

        bool IsTouchingWall { get; }

        bool IsTouchingCeiling { get; }

        /// <summary>Wall or other obstruction directly in the direction of travel.</summary>
        bool IsBlockedAhead { get; }

        /// <summary>No floor beyond the next step. Patrols turn around on this.</summary>
        bool IsEdgeAhead { get; }

        /// <summary>True while knockback or a dash has taken control away from the AI.</summary>
        bool IsControlLocked { get; }

        /// <summary>Steer in a direction. Magnitude is ignored; use <paramref name="speedScale"/>.</summary>
        void Move(Vector2 direction, float speedScale = 1f);

        /// <summary>Steer toward a world position.</summary>
        void MoveTowards(Vector2 worldPosition, float speedScale = 1f);

        /// <summary>Stop steering. <paramref name="immediate"/> zeroes velocity instead of decelerating.</summary>
        void Stop(bool immediate = false);

        /// <summary>Overwrite velocity outright. For scripted moves and cutscenes.</summary>
        void SetVelocity(Vector2 velocity);

        /// <summary>Turn to face a direction, without moving.</summary>
        void FaceDirection(int sign);

        /// <returns>False when jumping is impossible right now (airborne, or a flyer).</returns>
        bool TryJump(float force = 0f);

        /// <returns>False when a dash is already running or unsupported.</returns>
        bool TryDash(Vector2 direction, float force, float duration);

        /// <summary>Suspends physical motion, e.g. while burrowed or frozen.</summary>
        void SetMotionEnabled(bool motionEnabled);
    }
}
