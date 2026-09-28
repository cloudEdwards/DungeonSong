using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Chooses and runs attacks. The state machine asks "is there anything you can do to
    /// this target?" and this decides which of the enemy's attacks that is.
    /// <para>
    /// Selection is weighted random among everything currently usable, which makes a
    /// multi-attack enemy read as unpredictable without any per-enemy scripting. Combos come
    /// from an attack naming its own follow-up.
    /// </para>
    /// </summary>
    public class AttackController : EnemyModule
    {
        [Header("Global Pacing")]
        [SerializeField, Min(0f), Tooltip("Minimum seconds between any two attacks, on top of each attack's own cooldown. This is the enemy's overall aggression dial.")]
        private float globalCooldown = 0.5f;

        [SerializeField, Tooltip("Allow combos, where an attack chains into its defined follow-up.")]
        private bool allowFollowUps = true;

        [SerializeField, Min(0), Tooltip("Maximum attacks in one chain, as a guard against follow-up loops.")]
        private int maxChainLength = 4;

        /// <summary>Raised when an attack begins.</summary>
        public event Action<AttackBehaviour> AttackStarted;

        /// <summary>Raised when an attack ends, whether completed or cancelled.</summary>
        public event Action<AttackBehaviour> AttackFinished;

        private readonly List<AttackBehaviour> attacks = new List<AttackBehaviour>(4);
        private readonly List<AttackBehaviour> usableBuffer = new List<AttackBehaviour>(4);

        private AttackBehaviour current;
        private float globalCooldownTimer;
        private int chainLength;

        public override int TickOrder => ModuleTickOrder.Attacks;

        /// <summary>Every attack component on this enemy.</summary>
        public IReadOnlyList<AttackBehaviour> Attacks => attacks;

        public AttackBehaviour CurrentAttack => current;

        public bool IsAttacking => current != null && current.IsRunning;

        /// <summary>Whether the running attack's definition allows it to be interrupted.</summary>
        public bool IsCurrentAttackInterruptible =>
            current == null || current.Definition == null || current.Definition.Interruptible;

        public float GlobalCooldownRemaining => Mathf.Max(0f, globalCooldownTimer);

        protected override void OnBind() => GetComponents(attacks);

        public override void OnEnemyInitialized()
        {
            for (int i = 0; i < attacks.Count; i++)
            {
                attacks[i].Finished += OnAttackFinished;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i] != null)
                {
                    attacks[i].Finished -= OnAttackFinished;
                }
            }
        }

        public override void OnEnemySpawned()
        {
            current = null;
            globalCooldownTimer = 0f;
            chainLength = 0;
        }

        public override void Tick(float deltaTime)
        {
            if (globalCooldownTimer > 0f)
            {
                globalCooldownTimer -= deltaTime;
            }
        }

        /// <summary>True when at least one attack could be used against <paramref name="target"/> right now.</summary>
        public bool HasUsableAttack(ITargetable target)
        {
            if (target == null || globalCooldownTimer > 0f || IsAttacking)
            {
                return false;
            }

            for (int i = 0; i < attacks.Count; i++)
            {
                AttackBehaviour attack = attacks[i];
                if (attack.enabled && attack.CanUse(target))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Picks a usable attack and starts it.
        /// </summary>
        /// <returns>False when nothing was usable.</returns>
        public bool TryBeginAttack(ITargetable target)
        {
            if (target == null || IsAttacking || globalCooldownTimer > 0f)
            {
                return false;
            }

            AttackBehaviour chosen = SelectAttack(target);
            if (chosen == null)
            {
                return false;
            }

            chainLength = 1;
            BeginAttack(chosen, target);
            return true;
        }

        /// <summary>Runs a specific attack, bypassing selection. For scripted boss phases.</summary>
        public bool ForceAttack(AttackBehaviour attack, ITargetable target)
        {
            if (attack == null || !attack.IsReady)
            {
                return false;
            }

            chainLength = 1;
            BeginAttack(attack, target);
            return true;
        }

        public void CancelCurrentAttack()
        {
            if (current == null)
            {
                return;
            }

            AttackBehaviour cancelled = current;
            current = null;
            chainLength = 0;
            cancelled.Cancel();
        }

        private void BeginAttack(AttackBehaviour attack, ITargetable target)
        {
            current = attack;
            globalCooldownTimer = globalCooldown;
            attack.Begin(target);
            AttackStarted?.Invoke(attack);
        }

        /// <summary>
        /// Weighted random pick among usable attacks. Weighted rather than "first match" so
        /// an enemy with three attacks does not always open with the same one.
        /// </summary>
        private AttackBehaviour SelectAttack(ITargetable target)
        {
            usableBuffer.Clear();
            float totalWeight = 0f;

            for (int i = 0; i < attacks.Count; i++)
            {
                AttackBehaviour attack = attacks[i];
                if (!attack.enabled || !attack.CanUse(target))
                {
                    continue;
                }

                usableBuffer.Add(attack);
                totalWeight += attack.Definition != null ? Mathf.Max(0.01f, attack.Definition.SelectionWeight) : 1f;
            }

            if (usableBuffer.Count == 0)
            {
                return null;
            }

            if (usableBuffer.Count == 1)
            {
                return usableBuffer[0];
            }

            float roll = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < usableBuffer.Count; i++)
            {
                AttackBehaviour attack = usableBuffer[i];
                float weight = attack.Definition != null ? Mathf.Max(0.01f, attack.Definition.SelectionWeight) : 1f;
                roll -= weight;
                if (roll <= 0f)
                {
                    return attack;
                }
            }

            return usableBuffer[usableBuffer.Count - 1];
        }

        private void OnAttackFinished(AttackBehaviour attack)
        {
            if (attack != current)
            {
                return;
            }

            AttackFinished?.Invoke(attack);

            if (TryChainFollowUp(attack))
            {
                return;
            }

            current = null;
            chainLength = 0;
        }

        private bool TryChainFollowUp(AttackBehaviour finished)
        {
            if (!allowFollowUps || finished.Definition == null || finished.Definition.FollowUp == null)
            {
                return false;
            }

            if (chainLength >= Mathf.Max(1, maxChainLength))
            {
                return false;
            }

            if (UnityEngine.Random.value > finished.Definition.FollowUpChance)
            {
                return false;
            }

            ITargetable target = Owner.CurrentTarget;
            if (target == null || !target.IsValidTarget)
            {
                return false;
            }

            AttackBehaviour next = FindAttackByDefinition(finished.Definition.FollowUp);
            if (next == null || !next.IsReady || !next.CanUse(target))
            {
                return false;
            }

            chainLength++;
            current = next;

            // Follow-ups bypass the global cooldown; that is what makes a combo a combo.
            next.Begin(target);
            AttackStarted?.Invoke(next);
            return true;
        }

        /// <summary>Finds the component that runs a given attack asset.</summary>
        public AttackBehaviour FindAttackByDefinition(AttackDefinition wanted)
        {
            if (wanted == null)
            {
                return null;
            }

            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i].Definition == wanted)
                {
                    return attacks[i];
                }
            }

            return null;
        }

        // --- Animation relay. Forwarded to whichever attack is running. ---

        public void NotifyAnimationHitboxOn() => current?.AnimationHitboxOn();

        public void NotifyAnimationHitboxOff() => current?.AnimationHitboxOff();

        public void NotifyAnimationAttackComplete() => current?.AnimationComplete();

        public void NotifyAnimationSpawnProjectile() => current?.AnimationSpawnProjectile();
    }
}
