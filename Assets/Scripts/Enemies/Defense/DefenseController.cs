using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Collects every <see cref="IDamageModifier"/> on the enemy and runs them as a
    /// pipeline. <see cref="EnemyHealth"/> consults this before applying any damage, so
    /// adding a new kind of defense never means editing health code.
    /// Optional: an enemy without this component simply takes damage unmodified.
    /// </summary>
    public class DefenseController : EnemyModule
    {
        private readonly List<IDamageModifier> modifiers = new List<IDamageModifier>(4);

        /// <summary>Raised whenever defenses altered a hit. Use for block VFX and audio.</summary>
        public event Action<DamageInfo, DefenseEvaluation> Defended;

        public override int TickOrder => ModuleTickOrder.Health;

        /// <summary>True when any active defense would reject hits outright.</summary>
        public bool IsInvulnerable
        {
            get
            {
                DamageInfo probe = DamageInfo.Create(1f, DamageTeam.Player, transform.position);
                DefenseEvaluation evaluation = Evaluate(in probe, raiseEvent: false);
                return evaluation.Immune;
            }
        }

        protected override void OnBind()
        {
            // Cached once: the set of defense components on a prefab never changes at runtime.
            GetComponents(modifiers);
            modifiers.Sort(CompareOrder);
        }

        /// <summary>
        /// Registers a defense created at runtime (rare; most are authored on the prefab).
        /// </summary>
        public void Register(IDamageModifier modifier)
        {
            if (modifier == null || modifiers.Contains(modifier))
            {
                return;
            }

            modifiers.Add(modifier);
            modifiers.Sort(CompareOrder);
        }

        public void Unregister(IDamageModifier modifier) => modifiers.Remove(modifier);

        /// <summary>Runs every active defense over one hit and returns the combined verdict.</summary>
        public DefenseEvaluation Evaluate(in DamageInfo info, bool raiseEvent = true)
        {
            DefenseEvaluation evaluation = DefenseEvaluation.Default;

            for (int i = 0; i < modifiers.Count; i++)
            {
                IDamageModifier modifier = modifiers[i];
                if (modifier != null && modifier.IsActive)
                {
                    modifier.ModifyIncoming(in info, ref evaluation);
                }
            }

            bool altered = evaluation.Immune || evaluation.Blocked ||
                           !Mathf.Approximately(evaluation.Multiplier, 1f) ||
                           evaluation.FlatReduction > 0f;

            if (raiseEvent && altered)
            {
                Defended?.Invoke(info, evaluation);
            }

            return evaluation;
        }

        private static int CompareOrder(IDamageModifier a, IDamageModifier b) => a.ModifierOrder.CompareTo(b.ModifierOrder);
    }
}
