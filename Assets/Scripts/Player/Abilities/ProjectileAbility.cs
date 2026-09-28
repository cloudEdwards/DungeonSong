using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Combat.Projectiles;

namespace DungeonSong.Player
{
    /// <summary>
    /// Fires a projectile. This is the executor behind Eldritch Blast, and behind every
    /// future ranged spell or thrown tool: what differs between them is the
    /// <see cref="AbilityDefinition"/> and the <see cref="ProjectileDefinition"/>, not code.
    /// <para>
    /// It reuses the enemy-side projectile system wholesale, so player and enemy shots are
    /// pooled, damaged and resolved through exactly one implementation.
    /// </para>
    /// </summary>
    public class ProjectileAbility : AbilityBehaviour
    {
        [Header("Projectile")]
        [SerializeField, Tooltip("What to fire. Shared with enemies: the same asset type both sides use.")]
        private ProjectileDefinition projectile;

        [SerializeField, Tooltip("Where projectiles spawn. Leave empty to use the player's position.")]
        private Transform muzzle;

        [Header("Aiming")]
        [SerializeField, Tooltip("Allow aiming up and down with movement input, instead of only along facing.")]
        private bool allowVerticalAim = true;

        [SerializeField, Range(0.1f, 0.95f), Tooltip("How far the aim axis must be pushed to angle the shot.")]
        private float aimDeadzone = 0.5f;

        private PlayerInputRouter input;

        protected override void OnBind() => input = GetComponent<PlayerInputRouter>();

        private Vector2 MuzzlePosition
        {
            get
            {
                if (muzzle == null)
                {
                    return transform.position;
                }

                // Sprite flipping does not move child transforms, so mirror the muzzle to
                // the facing side before using it.
                Vector3 local = muzzle.localPosition;
                float wanted = Mathf.Abs(local.x) * Owner.FacingDirection;
                if (!Mathf.Approximately(local.x, wanted))
                {
                    local.x = wanted;
                    muzzle.localPosition = local;
                }

                return muzzle.position;
            }
        }

        protected override void OnResolve(in TargetInfo target)
        {
            if (projectile == null)
            {
                Debug.LogWarning($"ProjectileAbility on '{name}' has no ProjectileDefinition; nothing was fired.", this);
                return;
            }

            ProjectileSpawner.Fire(
                projectile,
                MuzzlePosition,
                ResolveAim(in target),
                Owner.Team,
                Owner.HostileTeams,
                Owner.gameObject);
        }

        private Vector2 ResolveAim(in TargetInfo target)
        {
            // Homing and nearest-enemy targeting already produced a direction; trust it.
            if (Definition.Targeting == TargetingMode.NearestEnemy && target.HasTarget)
            {
                return target.Direction;
            }

            var aim = new Vector2(Owner.FacingDirection, 0f);

            if (!allowVerticalAim || input?.Source == null)
            {
                return aim;
            }

            float vertical = input.Source.MoveAxis.y;
            if (Mathf.Abs(vertical) >= aimDeadzone)
            {
                // Straight up or down when there is no horizontal input, diagonal otherwise.
                float horizontal = Mathf.Abs(input.Source.MoveAxis.x) >= aimDeadzone ? Owner.FacingDirection : 0f;
                aim = new Vector2(horizontal, Mathf.Sign(vertical));
            }

            return aim.normalized;
        }

        private void OnDrawGizmosSelected()
        {
            if (Definition == null)
            {
                return;
            }

            Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.5f);
            Gizmos.DrawWireSphere(muzzle != null ? muzzle.position : transform.position, Definition.Range);
        }
    }
}
