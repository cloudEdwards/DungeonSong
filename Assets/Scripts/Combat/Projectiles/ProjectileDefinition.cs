using UnityEngine;

namespace DungeonSong.Combat.Projectiles
{
    /// <summary>How a projectile moves once launched.</summary>
    public enum ProjectileMotion
    {
        /// <summary>Constant velocity, no gravity.</summary>
        Straight = 0,

        /// <summary>Launched along the aim direction and pulled down by gravity.</summary>
        Arcing,

        /// <summary>Steers toward the target it was fired at.</summary>
        Homing,

        /// <summary>Flies out, stops, and returns to the shooter.</summary>
        Returning,
    }

    /// <summary>
    /// Everything designer-tunable about one kind of projectile. Shared between every
    /// attack that fires it, so a spit, a spine and a boss orb are three assets rather
    /// than three classes.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileDefinition", menuName = "Dungeon/Combat/Projectile Definition")]
    public class ProjectileDefinition : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Prefab with a Projectile component. Pooled automatically.")]
        public GameObject Prefab;

        [Tooltip("Instances to create up front the first time this definition is used.")]
        [Min(0)] public int PrewarmCount = 4;

        [Header("Motion")]
        public ProjectileMotion Motion = ProjectileMotion.Straight;

        [Min(0f), Tooltip("Launch speed in units/second.")]
        public float Speed = 8f;

        [Min(0f), Tooltip("Gravity scale. Only used by Arcing motion.")]
        public float GravityScale = 1.5f;

        [Min(0f), Tooltip("Degrees/second of steering. Only used by Homing motion.")]
        public float HomingTurnRate = 180f;

        [Min(0.05f), Tooltip("Seconds before the projectile despawns on its own.")]
        public float Lifetime = 4f;

        [Tooltip("Seconds of outward travel before a Returning projectile turns around.")]
        [Min(0f)] public float OutboundDuration = 0.6f;

        [Header("Damage")]
        [Min(0f)] public float Damage = 10f;

        [Min(0f), Tooltip("Poise damage. Drives whether the hit staggers its victim.")]
        public float PoiseDamage = 10f;

        public DamageType DamageType = DamageType.Projectile;

        [Min(0f)] public float KnockbackForce = 5f;

        [Range(0f, 1f), Tooltip("Upward bias added to knockback so victims pop rather than slide.")]
        public float KnockbackUpwardBias = 0.2f;

        [Header("Impact")]
        [Min(0), Tooltip("Extra targets the projectile can pass through. 0 = despawns on first hit.")]
        public int PierceCount;

        [Tooltip("Despawn when hitting terrain.")]
        public bool DespawnOnTerrain = true;

        [Header("Volley")]
        [Min(1), Tooltip("Projectiles fired per shot.")]
        public int ProjectilesPerShot = 1;

        [Min(0f), Tooltip("Total spread in degrees, distributed across the volley.")]
        public float SpreadDegrees;
    }
}
