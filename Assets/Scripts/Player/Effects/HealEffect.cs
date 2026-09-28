using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>
    /// Restores health to the target, or to the caster when there is no target.
    /// <para>
    /// This is the whole of Cure Wounds' consequence. A ranged heal, an area heal or a
    /// regeneration would reuse this effect and change only the ability's targeting.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "HealEffect", menuName = "Dungeon/Player/Effects/Heal")]
    public class HealEffect : GameplayEffect
    {
        [Header("Healing")]
        [Min(0f), Tooltip("Flat health restored.")]
        public float Amount = 25f;

        [Range(0f, 1f), Tooltip("Additional healing as a fraction of the target's maximum health.")]
        public float PercentOfMax;

        public override void Apply(in EffectContext context)
        {
            GameObject subject = context.Target != null ? context.Target : context.Source;
            if (subject == null)
            {
                return;
            }

            var health = subject.GetComponentInParent<IHealth>();
            if (health == null || !health.IsAlive)
            {
                return;
            }

            float total = Amount + health.Max * PercentOfMax;
            health.Heal(total);
        }
    }
}
