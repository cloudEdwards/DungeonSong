using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Combat.Projectiles;

namespace DungeonSong.Enemies
{
    /// <summary>What drives an attack's phase transitions.</summary>
    public enum AttackTimingSource
    {
        /// <summary>Numeric startup/active/recovery times from this asset. No clip authoring needed.</summary>
        Definition = 0,

        /// <summary>Animation clip events via <see cref="AnimationEventRelay"/>. Frame-accurate.</summary>
        AnimationEvents,
    }

    /// <summary>Optional movement an attack applies to its owner.</summary>
    public enum AttackMotion
    {
        None = 0,

        /// <summary>Short horizontal push, for a stepping slash.</summary>
        Lunge,

        /// <summary>Arcing jump toward the target.</summary>
        Leap,

        /// <summary>Long committed rush, for a charging beast.</summary>
        Dash,
    }

    /// <summary>
    /// One attack, as data. A sword slash, a spit, a leap and a boss slam are all this
    /// asset with different numbers, which is why "add an attack" is usually an asset and
    /// not a class.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackDefinition", menuName = "Dungeon/Enemies/Attack Definition")]
    public class AttackDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Attack";

        [Header("Timing")]
        [Tooltip("Numeric phases, or animation clip events.")]
        public AttackTimingSource TimingSource = AttackTimingSource.Definition;

        [Min(0f), Tooltip("Wind-up before the attack can hit. This is the player's tell; do not set it to zero.")]
        public float Startup = 0.35f;

        [Min(0f), Tooltip("Seconds the hitbox stays live.")]
        public float Active = 0.15f;

        [Min(0f), Tooltip("Seconds of vulnerability after the active frames. This is the punish window.")]
        public float Recovery = 0.4f;

        [Min(0f), Tooltip("Seconds before this attack can be used again.")]
        public float Cooldown = 1.5f;

        [Header("Usage Conditions")]
        [Min(0f), Tooltip("Closest distance at which this attack may be chosen.")]
        public float MinRange;

        [Min(0f), Tooltip("Furthest distance at which this attack may be chosen.")]
        public float MaxRange = 1.5f;

        [Tooltip("Require an unobstructed line to the target.")]
        public bool RequiresLineOfSight;

        [Tooltip("Require the attacker to be on the ground.")]
        public bool RequiresGrounded;

        [Range(0f, 180f), Tooltip("Maximum angle between facing and the target. 180 allows any direction.")]
        public float FacingTolerance = 90f;

        [Min(0.01f), Tooltip("Relative likelihood of being chosen when several attacks are usable.")]
        public float SelectionWeight = 1f;

        [Header("Damage")]
        [Min(0f)] public float Damage = 10f;

        [Min(0f), Tooltip("Poise damage dealt to the victim.")]
        public float PoiseDamage = 10f;

        public DamageType DamageType = DamageType.Physical;

        [Tooltip("Modifiers that let this attack bypass parts of the victim's defenses.")]
        public DamageFlags DamageFlags = DamageFlags.None;

        [Min(0f)] public float KnockbackForce = 6f;

        [Range(0f, 1f), Tooltip("Upward bias added to knockback.")]
        public float KnockbackUpwardBias = 0.25f;

        [Header("Hitbox")]
        [Tooltip("Key of the Hitbox on the prefab to activate. Melee attacks need this.")]
        public string HitboxKey = "Primary";

        [Min(0), Tooltip("Maximum victims per swing. 0 = unlimited.")]
        public int MaxTargets = 1;

        [Header("Projectile")]
        [Tooltip("Projectile to fire. Leave empty for a melee attack.")]
        public ProjectileDefinition Projectile;

        [Header("Motion")]
        public AttackMotion Motion = AttackMotion.None;

        [Min(0f), Tooltip("Speed of the attack's motion, if any.")]
        public float MotionForce = 8f;

        [Min(0f), Tooltip("Duration of the attack's motion, if any.")]
        public float MotionDuration = 0.2f;

        [Header("Interruption")]
        [Tooltip("Can higher-priority states cut this attack short? Off makes it committed, so the player can punish it.")]
        public bool Interruptible;

        [Header("Chaining")]
        [Tooltip("Attack to run immediately after this one, for combos. Beware of loops.")]
        public AttackDefinition FollowUp;

        [Range(0f, 1f), Tooltip("Chance the follow-up actually happens.")]
        public float FollowUpChance = 1f;

        [Header("Presentation")]
        [Tooltip("Semantic animation key, resolved by the enemy's AnimatorAdapter.")]
        public string AnimationKey = "attack";

        /// <summary>Total time from start to the end of recovery.</summary>
        public float TotalDuration => Startup + Active + Recovery;
    }
}
