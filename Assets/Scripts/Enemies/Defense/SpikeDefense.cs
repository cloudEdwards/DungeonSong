using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// The "curl into a spiky shell" defense: heavily resistant while active, and it
    /// hurts whoever strikes it. Composed rather than special-cased, so any enemy can be
    /// given a shell by adding this component and a state that switches it on.
    /// </summary>
    public class SpikeDefense : DefenseBehaviour
    {
        [Header("Mitigation")]
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of damage that gets through while shelled. 0 = immune.")]
        private float damageMultiplier;

        [SerializeField, Tooltip("Reject hits outright rather than reducing them.")]
        private bool fullyImmune = true;

        [Header("Retaliation")]
        [SerializeField, Min(0f), Tooltip("Damage dealt back to anything that strikes the shell.")]
        private float spikeDamage = 8f;

        [Header("Contact")]
        [SerializeField, Tooltip("Optional contact damager enabled only while the shell is up.")]
        private ContactDamager shellContactDamage;

        [Header("Presentation")]
        [SerializeField, Tooltip("Animation key played when the shell closes.")]
        private string enterAnimationKey = "shell_enter";

        [SerializeField, Tooltip("Animation key played when the shell opens.")]
        private string exitAnimationKey = "shell_exit";

        public override int ModifierOrder => 50;

        public override void ModifyIncoming(in DamageInfo info, ref DefenseEvaluation evaluation)
        {
            if (fullyImmune)
            {
                evaluation.Immune = true;
            }
            else
            {
                evaluation.Multiplier *= damageMultiplier;
            }

            evaluation.SuppressStagger = true;
            evaluation.SuppressKnockback = true;
            evaluation.ReflectDamage += spikeDamage;
        }

        protected override void OnActiveChanged(bool isActive)
        {
            if (shellContactDamage != null)
            {
                shellContactDamage.Active = isActive;
            }

            string key = isActive ? enterAnimationKey : exitAnimationKey;
            if (Owner != null && !string.IsNullOrEmpty(key))
            {
                Owner.Animation.PlayAction(key);
            }
        }
    }
}
