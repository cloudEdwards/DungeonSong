using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Passive damage reduction, plus the option to shrug off stagger from weak hits.
    /// This is the "armoured bug" variant: same prefab, one extra component.
    /// </summary>
    public class ArmorDefense : DefenseBehaviour
    {
        [Header("Reduction")]
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of damage that gets through. 0.5 halves incoming damage.")]
        private float damageMultiplier = 0.5f;

        [SerializeField, Min(0f), Tooltip("Flat damage subtracted after the multiplier.")]
        private float flatReduction;

        [Header("Stagger")]
        [SerializeField, Min(0f), Tooltip("Hits with poise damage below this never stagger. 0 disables the check.")]
        private float ignoreStaggerBelowPoise = 8f;

        [Header("Damage Types")]
        [SerializeField, Tooltip("Damage types this armour applies to. Others pass through untouched.")]
        private DamageType appliesTo = DamageType.Physical | DamageType.Contact | DamageType.Projectile;

        public override int ModifierOrder => 10;

        public override void ModifyIncoming(in DamageInfo info, ref DefenseEvaluation evaluation)
        {
            if ((info.Type & appliesTo) == 0)
            {
                return;
            }

            evaluation.Multiplier *= damageMultiplier;
            evaluation.FlatReduction += flatReduction;

            if (ignoreStaggerBelowPoise > 0f && info.PoiseDamage < ignoreStaggerBelowPoise)
            {
                evaluation.SuppressStagger = true;
            }
        }
    }
}
