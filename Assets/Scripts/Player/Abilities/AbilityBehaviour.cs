using System;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>An ability the player can activate.</summary>
    public interface IPlayerAbility
    {
        AbilityDefinition Definition { get; }

        bool IsReady { get; }

        bool IsRunning { get; }

        float CooldownRemaining { get; }

        /// <summary>Charges left, or -1 when the ability has unlimited uses.</summary>
        int ChargesRemaining { get; }

        /// <summary>Cost, cooldown, charges and context all permit activation right now.</summary>
        bool CanActivate();

        /// <summary>Starts the ability. Returns false when it could not be activated.</summary>
        bool TryActivate();

        /// <summary>Aborts a running ability and makes safe.</summary>
        void Cancel();
    }

    /// <summary>
    /// Shared machinery for every ability: cost, cooldown, charges, cast timing, movement
    /// lock, animation and interruption.
    /// <para>
    /// Subclasses implement only <see cref="OnResolve"/> — the moment the ability actually
    /// does its thing. That is why a projectile spell and a heal are a dozen lines each.
    /// </para>
    /// </summary>
    public abstract class AbilityBehaviour : PlayerModule, IPlayerAbility
    {
        [Header("Ability")]
        [SerializeField, Tooltip("The data for this ability: cost, cooldown, targeting, effects.")]
        private AbilityDefinition definition;

        /// <summary>Raised when the ability starts casting.</summary>
        public event Action<AbilityBehaviour> Activated;

        /// <summary>Raised when the ability finishes or is cancelled.</summary>
        public event Action<AbilityBehaviour> Finished;

        private float cooldownTimer;
        private float phaseTimer;
        private int charges = -1;
        private bool resolving;
        private bool movementLocked;
        private bool suspended;
        private float suspendedGravityScale;

        public override int TickOrder => PlayerTickOrder.Abilities;

        public AbilityDefinition Definition => definition;

        public bool IsRunning { get; private set; }

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

        public int ChargesRemaining => charges;

        public bool IsReady => definition != null && !IsRunning && cooldownTimer <= 0f && HasCharge;

        private bool HasCharge => definition == null || definition.MaxCharges <= 0 || charges > 0;

        /// <summary>
        /// A live counter for the HUD while the ability's effect persists, e.g. empowered
        /// strikes left on Divine Smite. 0 hides it.
        /// </summary>
        public virtual int ActiveStacks => 0;

        public override void OnPlayerInitialized()
        {
            ResetCharges();
            Owner.Damaged += OnOwnerDamaged;
        }

        protected virtual void OnDestroy()
        {
            if (Owner != null)
            {
                Owner.Damaged -= OnOwnerDamaged;
            }
        }

        // Only the wind-up can be interrupted: once the ability has resolved it has happened.
        private void OnOwnerDamaged(PlayerActor player, DamageInfo info, DamageResult result)
        {
            if (IsRunning && !resolving && result.Applied && definition != null && definition.InterruptedByDamage)
            {
                Cancel();
            }
        }

        public override void OnPlayerSpawned()
        {
            Cancel();
            cooldownTimer = 0f;
            ResetCharges();
        }

        public override void OnPlayerDied() => Cancel();

        /// <summary>Refills charges. Called on spawn and by rest points.</summary>
        public void ResetCharges()
        {
            charges = definition != null && definition.MaxCharges > 0 ? definition.MaxCharges : -1;
        }

        public virtual bool CanActivate()
        {
            if (!IsReady)
            {
                return false;
            }

            if (Owner.Resources != null && !Owner.Resources.CanAfford(definition.Cost))
            {
                return false;
            }

            return AreRequirementsMet(out _);
        }

        /// <summary>
        /// Checks every requirement on the definition. <paramref name="unmetMessage"/> is
        /// the first failure's message, so UI can tell the player why nothing happened.
        /// </summary>
        public bool AreRequirementsMet(out string unmetMessage)
        {
            unmetMessage = null;

            if (definition == null || definition.Requirements == null)
            {
                return true;
            }

            for (int i = 0; i < definition.Requirements.Length; i++)
            {
                AbilityRequirement requirement = definition.Requirements[i];
                if (requirement != null && !requirement.IsSatisfied(Owner))
                {
                    unmetMessage = requirement.UnmetMessage;
                    return false;
                }
            }

            return true;
        }

        public bool TryActivate()
        {
            if (!CanActivate())
            {
                return false;
            }

            if (!definition.SpendCostOnResolve && Owner.Resources != null && !Owner.Resources.TrySpend(definition.Cost))
            {
                return false;
            }

            if (definition.MaxCharges > 0)
            {
                charges--;
            }

            cooldownTimer = definition.Cooldown;
            IsRunning = true;
            resolving = false;
            phaseTimer = definition.CastTime;

            if (definition.LockMovement)
            {
                Owner.PushMovementLock();
                movementLocked = true;
            }

            if (definition.SuspendInAir)
            {
                BeginSuspend();
            }

            Owner.Animation.PlayAction(definition.AnimationKey);
            OnActivated();
            Activated?.Invoke(this);
            return true;
        }

        public override void Tick(float deltaTime)
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= deltaTime;
            }

            if (!IsRunning)
            {
                return;
            }

            if (suspended)
            {
                Owner.Body.linearVelocity = Vector2.zero;
            }

            phaseTimer -= deltaTime;
            if (phaseTimer > 0f)
            {
                return;
            }

            if (!resolving)
            {
                if (definition.SpendCostOnResolve && Owner.Resources != null && !Owner.Resources.TrySpend(definition.Cost))
                {
                    Cancel();
                    return;
                }

                resolving = true;
                phaseTimer = definition.Recovery;
                Resolve();
                return;
            }

            Finish();
        }

        /// <summary>
        /// Runs the ability's payload: resolves targeting, hands control to the subclass,
        /// then applies the definition's effects.
        /// </summary>
        private void Resolve()
        {
            TargetInfo target = TargetingResolver.Resolve(definition.Targeting, Owner, definition.Range);
            OnResolve(in target);
            ApplyEffects(in target);
        }

        /// <summary>
        /// Applies every effect on the definition. Subclasses that deliver their effects
        /// some other way (a projectile carries them to the victim) can skip this.
        /// </summary>
        protected virtual void ApplyEffects(in TargetInfo target)
        {
            if (definition.Effects == null || definition.Effects.Length == 0)
            {
                return;
            }

            var context = new EffectContext(
                Owner.gameObject,
                Owner.Team,
                target.Position,
                target.Direction,
                target.Target);

            for (int i = 0; i < definition.Effects.Length; i++)
            {
                if (definition.Effects[i] != null)
                {
                    definition.Effects[i].Apply(in context);
                }
            }
        }

        public void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            EndRun();
            OnCancelled();
            Finished?.Invoke(this);
        }

        private void Finish()
        {
            EndRun();
            OnFinished();
            Finished?.Invoke(this);
        }

        private void EndRun()
        {
            IsRunning = false;
            resolving = false;
            phaseTimer = 0f;

            if (movementLocked)
            {
                Owner.PopMovementLock();
                movementLocked = false;
            }

            EndSuspend();
        }

        /// <summary>
        /// Hangs an airborne player in place by switching gravity off, until the ability
        /// ends. Grounded casts are left alone.
        /// </summary>
        private void BeginSuspend()
        {
            if (suspended || Owner.Body == null || Owner.Motion == null || Owner.Motion.IsGrounded)
            {
                return;
            }

            suspended = true;
            suspendedGravityScale = Owner.Body.gravityScale;
            Owner.Body.gravityScale = 0f;
            Owner.Body.linearVelocity = Vector2.zero;
        }

        private void EndSuspend()
        {
            if (!suspended)
            {
                return;
            }

            suspended = false;
            if (Owner.Body != null)
            {
                Owner.Body.gravityScale = suspendedGravityScale;
            }
        }

        // --- Subclass hooks ---

        /// <summary>Called the moment the ability starts casting.</summary>
        protected virtual void OnActivated() { }

        /// <summary>
        /// The ability happens now. Fire the projectile, open the portal, plant the trap.
        /// Effects on the definition are applied immediately afterwards.
        /// </summary>
        protected abstract void OnResolve(in TargetInfo target);

        protected virtual void OnFinished() { }

        protected virtual void OnCancelled() { }
    }
}
