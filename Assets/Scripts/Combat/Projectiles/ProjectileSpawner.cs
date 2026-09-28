using UnityEngine;
using DungeonSong.Combat.Pooling;

namespace DungeonSong.Combat.Projectiles
{
    /// <summary>
    /// Single entry point for firing a <see cref="ProjectileDefinition"/>. Keeping the
    /// volley and pooling logic here means attack components never touch Instantiate.
    /// </summary>
    public static class ProjectileSpawner
    {
        /// <summary>
        /// Fires one shot, which may be a volley of several projectiles.
        /// </summary>
        /// <param name="definition">What to fire.</param>
        /// <param name="origin">Muzzle position.</param>
        /// <param name="aimDirection">Direction to fire in; normalized internally.</param>
        /// <param name="sourceTeam">Team of the shooter.</param>
        /// <param name="targetTeams">Teams the projectiles may damage.</param>
        /// <param name="source">Shooter GameObject, used for attribution and returning shots.</param>
        /// <param name="target">Optional target for homing motion.</param>
        public static void Fire(ProjectileDefinition definition, Vector2 origin, Vector2 aimDirection, DamageTeam sourceTeam, DamageTeam targetTeams, GameObject source, ITargetable target = null)
        {
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning("ProjectileSpawner.Fire called with no projectile prefab.");
                return;
            }

            if (definition.PrewarmCount > 0 && PrefabPool.CountInactive(definition.Prefab) == 0)
            {
                PrefabPool.Prewarm(definition.Prefab, definition.PrewarmCount);
            }

            int count = Mathf.Max(1, definition.ProjectilesPerShot);
            float spread = definition.SpreadDegrees;
            float step = count > 1 ? spread / (count - 1) : 0f;
            float start = count > 1 ? -spread * 0.5f : 0f;

            for (int i = 0; i < count; i++)
            {
                Vector2 dir = count > 1
                    ? (Vector2)(Quaternion.Euler(0f, 0f, start + step * i) * aimDirection)
                    : aimDirection;

                GameObject instance = PrefabPool.Get(definition.Prefab, origin, Quaternion.identity);
                if (instance == null)
                {
                    continue;
                }

                if (instance.TryGetComponent(out Projectile projectile))
                {
                    projectile.Launch(definition, dir, sourceTeam, targetTeams, source, target);
                }
                else
                {
                    Debug.LogWarning($"Projectile prefab '{definition.Prefab.name}' has no Projectile component.", definition.Prefab);
                    PrefabPool.Release(instance);
                }
            }
        }
    }
}
