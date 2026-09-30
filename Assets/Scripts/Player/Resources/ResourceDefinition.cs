using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Kinds of rest, D&amp;D style. A short rest is the Loyalty-fuelled bind taken anywhere;
    /// a long rest is a campfire.
    /// </summary>
    [System.Flags]
    public enum RestType
    {
        None = 0,
        Short = 1 << 0,
        Long = 1 << 1,
    }

    /// <summary>
    /// A spendable pool: Loyalty, spell slots, tool charges, stamina.
    /// <para>
    /// Deliberately not an enum. Adding a resource later must be an asset a designer
    /// creates, not a code change that ripples through every ability that switches on it.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Resource", menuName = "Dungeon/Player/Resource Definition")]
    public class ResourceDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Resource";

        [Tooltip("Stable id for saves and UI. Changing it orphans saved values.")]
        public string Id = "mana";

        [Header("Pool")]
        [Min(0f)] public float MaxAmount = 100f;

        [Tooltip("Amount the pool starts at and returns to on a full restore.")]
        [Min(0f)] public float StartingAmount = 100f;

        [Header("Regeneration")]
        [Min(0f), Tooltip("Amount restored per second. 0 means it only refills from rest points and pickups.")]
        public float RegenPerSecond;

        [Min(0f), Tooltip("Seconds after spending before regeneration resumes.")]
        public float RegenDelay = 1f;

        [Header("Recovery")]
        [Tooltip("Rests that refill this pool to max. Warlock slots: Short + Long. Paladin slots: Long. Loyalty: none.")]
        public RestType RefilledBy = RestType.Long;

        [Tooltip("Empty this pool when the player dies, like Silksong's silk.")]
        public bool EmptiedOnDeath;
    }

    /// <summary>What an attack or ability costs to use.</summary>
    [System.Serializable]
    public struct ResourceCost
    {
        [Tooltip("Resource spent. Leave empty for a free action.")]
        public ResourceDefinition Resource;

        [Min(0f)] public float Amount;

        public bool IsFree => Resource == null || Amount <= 0f;
    }
}
