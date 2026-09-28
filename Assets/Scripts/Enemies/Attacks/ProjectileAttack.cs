using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Combat.Projectiles;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Fires the projectile named by its <see cref="AttackDefinition"/>. Leading the target
    /// and aiming tolerance live here; everything about the projectile itself lives in its
    /// own definition asset, so the same shot can be reused by several enemies.
    /// </summary>
    public class ProjectileAttack : AttackBehaviour
    {
        [Header("Muzzle")]
        [SerializeField, Tooltip("Where projectiles spawn. Leave empty to use this transform.")]
        private Transform muzzle;

        [SerializeField, Tooltip("Teams the projectiles can damage.")]
        private DamageTeam targetTeams = DamageTeam.Player;

        [Header("Aiming")]
        [SerializeField, Tooltip("Aim at the target. Off fires straight ahead, which suits a fixed turret.")]
        private bool aimAtTarget = true;

        [SerializeField, Range(0f, 1f), Tooltip("How much the shot leads a moving target. 0 aims where it is, 1 where it will be.")]
        private float targetLeading;

        [SerializeField, Tooltip("Rigidbody of the target used for leading. Resolved from the target at fire time.")]
        private bool leadUsingTargetVelocity = true;

        private Vector2 lastTargetPosition;
        private float lastTargetSampleTime;

        // Mirrored to the owner's facing, so a left-facing enemy does not spit out of its back.
        private Vector2 MuzzlePosition => muzzle != null ? ResolveAttachment(muzzle) : (Vector2)transform.position;

        protected override void OnAttackBegin()
        {
            if (CurrentTarget != null)
            {
                lastTargetPosition = CurrentTarget.AimPosition;
                lastTargetSampleTime = Time.time;
            }
        }

        protected override void OnActiveBegin()
        {
            // With animation-event timing the clip decides the exact frame instead.
            if (Definition.TimingSource == AttackTimingSource.Definition)
            {
                Fire();
            }
        }

        protected override void OnActiveEnd() { }

        internal override void AnimationSpawnProjectile() => Fire();

        /// <summary>Spawns the shot. Public so a boss script can fire outside the normal phases.</summary>
        public void Fire()
        {
            if (Definition == null || Definition.Projectile == null)
            {
                Debug.LogWarning($"ProjectileAttack on '{name}' has no ProjectileDefinition assigned.", this);
                return;
            }

            ProjectileSpawner.Fire(
                Definition.Projectile,
                MuzzlePosition,
                ComputeAimDirection(),
                Owner.Team,
                targetTeams,
                Owner.gameObject,
                CurrentTarget);
        }

        private Vector2 ComputeAimDirection()
        {
            if (!aimAtTarget || CurrentTarget == null)
            {
                return new Vector2(Owner.FacingDirection, 0f);
            }

            Vector2 aimPoint = CurrentTarget.AimPosition;

            if (targetLeading > 0f && leadUsingTargetVelocity)
            {
                aimPoint += EstimateTargetVelocity() * (targetLeading * EstimateTimeToTarget(aimPoint));
            }

            Vector2 direction = aimPoint - MuzzlePosition;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : new Vector2(Owner.FacingDirection, 0f);
        }

        private Vector2 EstimateTargetVelocity()
        {
            // Sampled rather than read off a Rigidbody2D, so it works for any target
            // implementation, including ones that move by transform.
            if (CurrentTarget == null)
            {
                return Vector2.zero;
            }

            float dt = Time.time - lastTargetSampleTime;
            Vector2 current = CurrentTarget.AimPosition;

            if (dt <= 0.0001f)
            {
                return Vector2.zero;
            }

            Vector2 velocity = (current - lastTargetPosition) / dt;
            lastTargetPosition = current;
            lastTargetSampleTime = Time.time;
            return velocity;
        }

        private float EstimateTimeToTarget(Vector2 aimPoint)
        {
            float speed = Definition.Projectile.Speed;
            return speed > 0.01f ? Vector2.Distance(MuzzlePosition, aimPoint) / speed : 0f;
        }

        private void OnDrawGizmosSelected()
        {
            if (Definition == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(MuzzlePosition, Definition.MaxRange);

            if (Definition.MinRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
                Gizmos.DrawWireSphere(MuzzlePosition, Definition.MinRange);
            }
        }
    }
}
