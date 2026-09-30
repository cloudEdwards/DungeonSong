using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Broad category, used for progression, UI grouping and unlock rules rather than by
    /// the runtime. A tool is a category of ability, not a separate system.
    /// </summary>
    [System.Flags]
    public enum AbilityCategory
    {
        None = 0,
        Spell = 1 << 0,
        Tool = 1 << 1,
        Melee = 1 << 2,
        Movement = 1 << 3,
        Defensive = 1 << 4,
        Healing = 1 << 5,
        Utility = 1 << 6,
    }

    /// <summary>
    /// One ability, as data: what it costs, how it aims, how long it takes, what it looks
    /// like. What it actually *does* lives in the effects it applies and in the
    /// <see cref="AbilityBehaviour"/> that executes it.
    /// <para>
    /// Eldritch Blast and Cure Wounds are both this asset. So is every spell and tool that
    /// follows, unless it needs genuinely new execution mechanics.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "Dungeon/Player/Ability Definition")]
    public class AbilityDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Ability";

        [Tooltip("Stable id for saves, unlocks and loadout persistence.")]
        public string Id = "ability";

        [TextArea(2, 4)] public string Description;

        public Sprite Icon;

        [Tooltip("Classification for UI and progression. Free-form combinations are fine.")]
        public AbilityCategory Category = AbilityCategory.Spell;

        [Tooltip("Free-form tags, e.g. 'paladin', 'warlock', 'holy', 'eldritch'.")]
        public string[] Tags = System.Array.Empty<string>();

        [Header("Cost")]
        [Tooltip("Resource spent on activation. Leave the resource empty for a free ability — a cantrip.")]
        public ResourceCost Cost;

        [Tooltip("Spend the cost when the ability takes effect instead of when the cast starts, so an interrupted cast costs nothing. The short rest uses this.")]
        public bool SpendCostOnResolve;

        [Min(0f), Tooltip("Seconds before this ability can be used again.")]
        public float Cooldown = 1f;

        [Header("Charges")]
        [Min(0), Tooltip("Limited uses between rests, for tools. 0 means unlimited.")]
        public int MaxCharges;

        [Header("Targeting")]
        public TargetingMode Targeting = TargetingMode.Direction;

        [Min(0f), Tooltip("Range used by NearestEnemy and PointInFront targeting.")]
        public float Range = 8f;

        [Header("Timing")]
        [Min(0f), Tooltip("Wind-up before the ability takes effect. The player's tell.")]
        public float CastTime = 0.2f;

        [Min(0f), Tooltip("Seconds after the effect during which the player is still committed.")]
        public float Recovery = 0.15f;

        [Tooltip("Root the player while casting.")]
        public bool LockMovement = true;

        [Tooltip("When cast in the air, hang in place instead of falling until the ability finishes. The short rest uses this, like Silksong's bind.")]
        public bool SuspendInAir;

        [Tooltip("Can taking damage interrupt this cast?")]
        public bool InterruptedByDamage = true;

        [Header("Requirements")]
        [Tooltip("Conditions that must all hold before this ability can be used. Empty means always available.")]
        public AbilityRequirement[] Requirements = System.Array.Empty<AbilityRequirement>();

        [Header("Effects")]
        [Tooltip("Applied when the ability resolves. Compose abilities from reusable effects rather than writing new behaviour.")]
        public GameplayEffect[] Effects = System.Array.Empty<GameplayEffect>();

        [Header("Presentation")]
        [Tooltip("Semantic animation key, resolved by the player's animator adapter.")]
        public string AnimationKey = "cast";
    }
}
