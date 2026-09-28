using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>How an ability decides what it acts on.</summary>
    public enum TargetingMode
    {
        /// <summary>The caster. Self-heals and buffs.</summary>
        Self = 0,

        /// <summary>A direction, taken from facing and aim input. Projectiles and beams.</summary>
        Direction,

        /// <summary>The closest valid hostile within range.</summary>
        NearestEnemy,

        /// <summary>A world position in front of the caster. Ground effects and placements.</summary>
        PointInFront,
    }

    /// <summary>The resolved answer to "what am I aiming at?"</summary>
    public readonly struct TargetInfo
    {
        public readonly bool HasTarget;
        public readonly GameObject Target;
        public readonly Vector2 Position;
        public readonly Vector2 Direction;

        public TargetInfo(bool hasTarget, GameObject target, Vector2 position, Vector2 direction)
        {
            HasTarget = hasTarget;
            Target = target;
            Position = position;
            Direction = direction;
        }
    }

    /// <summary>
    /// Turns a <see cref="TargetingMode"/> into a concrete target.
    /// <para>
    /// Kept separate from abilities so that changing "Cure Wounds" from self-only to a
    /// touch heal is a dropdown on the asset, not a rewrite of the ability.
    /// </para>
    /// </summary>
    public static class TargetingResolver
    {
        public static TargetInfo Resolve(TargetingMode mode, PlayerActor actor, float range)
        {
            Vector2 origin = actor.transform.position;
            Vector2 facing = new Vector2(actor.FacingDirection, 0f);

            switch (mode)
            {
                case TargetingMode.Self:
                    return new TargetInfo(true, actor.gameObject, origin, facing);

                case TargetingMode.Direction:
                    return new TargetInfo(true, null, origin, facing);

                case TargetingMode.NearestEnemy:
                {
                    ITargetable nearest = TargetRegistry.FindNearest(origin, actor.HostileTeams, range);
                    if (nearest == null)
                    {
                        return new TargetInfo(false, null, origin, facing);
                    }

                    Vector2 targetPosition = nearest.AimPosition;
                    Vector2 toTarget = (targetPosition - origin).normalized;
                    return new TargetInfo(true, nearest.Transform.gameObject, targetPosition, toTarget);
                }

                case TargetingMode.PointInFront:
                {
                    Vector2 point = origin + facing * range;
                    return new TargetInfo(true, null, point, facing);
                }

                default:
                    return new TargetInfo(false, null, origin, facing);
            }
        }
    }
}
