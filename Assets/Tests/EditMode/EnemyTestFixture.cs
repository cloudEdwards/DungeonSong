using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies.Tests
{
    /// <summary>
    /// Builds enemies in code for edit-mode tests.
    /// <para>
    /// Edit mode never runs Awake or Start, which is exactly why the framework exposes
    /// explicit <see cref="Enemy.Initialize"/> and <see cref="Enemy.Spawn()"/> calls rather
    /// than hiding that work in Unity callbacks. These helpers drive those calls.
    /// </para>
    /// </summary>
    internal static class EnemyTestFixture
    {
        /// <summary>Creates stats with predictable numbers, overridable per test.</summary>
        public static EnemyStats CreateStats(
            float maxHealth = 100f,
            float maxPoise = 20f,
            float hitInvulnerability = 0f,
            float staggerDuration = 0.5f,
            float knockbackResistance = 0f,
            float despawnDelay = 1f)
        {
            EnemyStats stats = ScriptableObject.CreateInstance<EnemyStats>();
            stats.MaxHealth = maxHealth;
            stats.MaxPoise = maxPoise;
            stats.HitInvulnerability = hitInvulnerability;
            stats.StaggerDuration = staggerDuration;
            stats.KnockbackResistance = knockbackResistance;
            stats.DespawnDelay = despawnDelay;
            stats.PoiseRegenDelay = 10f;
            stats.PoiseRegenRate = 0f;
            return stats;
        }

        /// <summary>
        /// Creates an enemy with health and whatever extra components a test asks for, then
        /// initializes and spawns it so every module has had its lifecycle callbacks.
        /// </summary>
        public static Enemy CreateEnemy(EnemyStats stats, params System.Type[] extraComponents)
        {
            var go = new GameObject("TestEnemy");
            go.AddComponent<Rigidbody2D>();

            Enemy enemy = go.AddComponent<Enemy>();
            EnemyHealth health = go.AddComponent<EnemyHealth>();

            if (extraComponents != null)
            {
                foreach (System.Type type in extraComponents)
                {
                    go.AddComponent(type);
                }
            }

            enemy.Initialize();
            health.Configure(stats);
            enemy.Spawn();
            return enemy;
        }

        /// <summary>A plain physical hit from the player's team, aimed from the left.</summary>
        public static DamageInfo Hit(float amount, float poiseDamage = -1f, float knockback = 0f)
        {
            DamageInfo info = DamageInfo.Create(amount, DamageTeam.Player, new Vector2(-1f, 0f));
            info.PoiseDamage = poiseDamage >= 0f ? poiseDamage : amount;
            info.KnockbackForce = knockback;
            return info;
        }

        /// <summary>Runs <paramref name="seconds"/> of module ticks in fixed steps.</summary>
        public static void Tick(EnemyModule module, float seconds, float step = 0.02f)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(seconds / step));
            for (int i = 0; i < steps; i++)
            {
                module.Tick(step);
            }
        }

        public static void Destroy(Enemy enemy)
        {
            if (enemy != null)
            {
                Object.DestroyImmediate(enemy.gameObject);
            }
        }
    }

    /// <summary>A target test doubles can aim at, with no player involved.</summary>
    internal class FakeTarget : MonoBehaviour, ITargetable
    {
        public DamageTeam TeamValue = DamageTeam.Player;
        public bool Valid = true;

        public Transform Transform => transform;

        public Vector2 AimPosition => transform.position;

        public DamageTeam Team => TeamValue;

        public bool IsValidTarget => Valid;

        public static FakeTarget Create(Vector2 position, bool register = true)
        {
            var go = new GameObject("FakeTarget");
            go.transform.position = position;
            FakeTarget target = go.AddComponent<FakeTarget>();

            if (register)
            {
                TargetRegistry.Register(target);
            }

            return target;
        }

        public void Dispose()
        {
            TargetRegistry.Unregister(this);
            DestroyImmediate(gameObject);
        }
    }
}
