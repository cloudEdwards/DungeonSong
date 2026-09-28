using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Walks on floors under gravity, with optional jumping and dashing. The workhorse
    /// movement for patrollers, chasers and chargers alike; what differs between those
    /// enemies is which states drive it, not this component.
    /// </summary>
    public class GroundMovement : MovementControllerBase
    {
        [Header("Ground Behaviour")]
        [SerializeField, Tooltip("Keep steering while airborne. Off gives committed, weighty jumps.")]
        private bool airControl = true;

        [SerializeField, Range(0f, 1f), Tooltip("Fraction of ground acceleration available in the air.")]
        private float airControlFactor = 0.4f;

        private GroundMovementSettings groundSettings;
        private float dashTimer;
        private float dashCooldownTimer;
        private Vector2 dashVelocity;

        /// <summary>True while a dash is in progress.</summary>
        public bool IsDashing => dashTimer > 0f;

        protected override void ConfigureBody()
        {
            groundSettings = Settings as GroundMovementSettings;

            if (Body == null)
            {
                return;
            }

            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.gravityScale = groundSettings != null ? groundSettings.GravityScale : 3f;
            Body.freezeRotation = true;
        }

        public override bool TryJump(float force = 0f)
        {
            if (!IsGrounded || Body == null || IsControlLocked)
            {
                return false;
            }

            float impulse = force > 0f ? force : groundSettings != null ? groundSettings.JumpForce : 7f;
            Body.linearVelocity = new Vector2(Body.linearVelocity.x, impulse);
            return true;
        }

        public override bool TryDash(Vector2 direction, float force, float duration)
        {
            if (Body == null || dashTimer > 0f || dashCooldownTimer > 0f)
            {
                return false;
            }

            float speed = force > 0f ? force : groundSettings != null ? groundSettings.DashForce : 10f;
            float time = duration > 0f ? duration : groundSettings != null ? groundSettings.DashDuration : 0.25f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = new Vector2(Owner != null ? Owner.FacingDirection : 1, 0f);
            }

            dashVelocity = direction.normalized * speed;
            dashTimer = time;
            dashCooldownTimer = time + (groundSettings != null ? groundSettings.DashCooldown : 1f);
            return true;
        }

        protected override void ApplyMotion(float fixedDeltaTime)
        {
            if (Body == null || Settings == null)
            {
                return;
            }

            if (dashCooldownTimer > 0f)
            {
                dashCooldownTimer -= fixedDeltaTime;
            }

            Vector2 velocity = Body.linearVelocity;

            // A dash overrides steering for its duration, then hands control back.
            if (dashTimer > 0f)
            {
                dashTimer -= fixedDeltaTime;
                velocity.x = dashVelocity.x;
                if (Mathf.Abs(dashVelocity.y) > 0.01f)
                {
                    velocity.y = dashVelocity.y;
                }

                Body.linearVelocity = velocity;
                return;
            }

            if (IsControlLocked)
            {
                return;
            }

            float targetSpeed = DesiredDirection.x * Settings.MoveSpeed * DesiredSpeedScale;
            bool accelerating = Mathf.Abs(targetSpeed) > 0.01f;
            float rate = accelerating ? Settings.Acceleration : Settings.Deceleration;

            if (!IsGrounded)
            {
                rate *= airControl ? airControlFactor : 0f;
            }

            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, rate * fixedDeltaTime);

            float maxFall = groundSettings != null ? groundSettings.MaxFallSpeed : 18f;
            if (velocity.y < -maxFall)
            {
                velocity.y = -maxFall;
            }

            Body.linearVelocity = velocity;
        }
    }
}
