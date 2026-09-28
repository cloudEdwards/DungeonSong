using System;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Shared machinery for every attack: the startup/active/recovery phase machine,
    /// cooldown, usage conditions and optional lunge or leap motion.
    /// <para>
    /// Subclasses implement only what their attack actually does, in
    /// <see cref="OnActiveBegin"/> and <see cref="OnActiveEnd"/>. That is the whole reason
    /// "sword attack" is not a concept in this framework: a sword is a melee attack asset
    /// whose hitbox happens to be shaped like a sword.
    /// </para>
    /// </summary>
    public abstract class AttackBehaviour : EnemyModule, IAttack
    {
        [Header("Definition")]
        [SerializeField, Tooltip("The data for this attack: timings, damage, range, animation key.")]
        private AttackDefinition definition;

        // Cheap unique id per attack component, used to tag hits so a victim can tell
        // two swings apart. Assigned from a counter rather than an instance id, which
        // Unity 6 no longer exposes as an int.
        private static int nextAttackId = 1;

        private int attackId;
        private AttackPhase phase;
        private float phaseTimer;
        private float cooldownTimer;
        private ITargetable currentTarget;
        private bool awaitingAnimation;

        /// <summary>Raised when the attack finishes or is cancelled.</summary>
        public event Action<AttackBehaviour> Finished;

        public override int TickOrder => ModuleTickOrder.Attacks;

        public AttackDefinition Definition => definition;

        public AttackPhase Phase => phase;

        public bool IsReady => definition != null && phase == AttackPhase.Ready && cooldownTimer <= 0f;

        public bool IsRunning => phase != AttackPhase.Ready;

        /// <summary>Seconds until this attack is available again.</summary>
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

        /// <summary>The target this swing was aimed at.</summary>
        protected ITargetable CurrentTarget => currentTarget;

        protected IAnimatorAdapter Anim => Owner.Animation;

        protected override void OnBind() => attackId = nextAttackId++;

        public override void OnEnemySpawned()
        {
            phase = AttackPhase.Ready;
            phaseTimer = 0f;
            cooldownTimer = 0f;
            currentTarget = null;
            awaitingAnimation = false;
        }

        public override void OnEnemyDespawned() => Cancel();

        public virtual bool CanUse(ITargetable target)
        {
            if (!IsReady || target == null || !target.IsValidTarget)
            {
                return false;
            }

            Vector2 toTarget = target.AimPosition - (Vector2)transform.position;
            float distance = toTarget.magnitude;

            if (distance < definition.MinRange || distance > definition.MaxRange)
            {
                return false;
            }

            if (definition.RequiresGrounded && Owner.Movement != null && !Owner.Movement.IsGrounded)
            {
                return false;
            }

            if (definition.FacingTolerance < 180f)
            {
                var facing = new Vector2(Owner.FacingDirection, 0f);
                if (Vector2.Angle(facing, toTarget.normalized) > definition.FacingTolerance)
                {
                    return false;
                }
            }

            if (definition.RequiresLineOfSight && Owner.Perception != null && !Owner.Perception.HasLineOfSight)
            {
                return false;
            }

            return true;
        }

        public void Begin(ITargetable target)
        {
            if (definition == null)
            {
                Debug.LogWarning($"{GetType().Name} on '{name}' has no AttackDefinition.", this);
                return;
            }

            currentTarget = target;
            Anim.PlayAction(definition.AnimationKey);
            ApplyMotion();
            EnterPhase(AttackPhase.Startup);
            OnAttackBegin();
        }

        public void Cancel()
        {
            if (phase == AttackPhase.Ready)
            {
                return;
            }

            if (phase == AttackPhase.Active)
            {
                OnActiveEnd();
            }

            OnAttackCancelled();
            phase = AttackPhase.Ready;
            awaitingAnimation = false;
            phaseTimer = 0f;
            StartCooldown();
            Finished?.Invoke(this);
        }

        public override void Tick(float deltaTime)
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= deltaTime;
            }

            if (phase == AttackPhase.Ready)
            {
                return;
            }

            // When animation events drive timing, the clip advances the phases and this
            // timer is only a safety net against a clip with missing events.
            if (awaitingAnimation)
            {
                phaseTimer -= deltaTime;
                if (phaseTimer <= 0f)
                {
                    AdvancePhase();
                }

                return;
            }

            phaseTimer -= deltaTime;
            if (phaseTimer <= 0f)
            {
                AdvancePhase();
            }
        }

        private void AdvancePhase()
        {
            switch (phase)
            {
                case AttackPhase.Startup:
                    EnterPhase(AttackPhase.Active);
                    break;

                case AttackPhase.Active:
                    EnterPhase(AttackPhase.Recovery);
                    break;

                case AttackPhase.Recovery:
                    Complete();
                    break;
            }
        }

        private void EnterPhase(AttackPhase next)
        {
            if (phase == AttackPhase.Active && next != AttackPhase.Active)
            {
                OnActiveEnd();
            }

            phase = next;
            bool animationDriven = definition.TimingSource == AttackTimingSource.AnimationEvents;

            switch (next)
            {
                case AttackPhase.Startup:
                    // The safety-net timeout is generous so a slow clip is never cut short.
                    phaseTimer = animationDriven ? definition.TotalDuration * 2f : definition.Startup;
                    awaitingAnimation = animationDriven;
                    break;

                case AttackPhase.Active:
                    phaseTimer = animationDriven ? definition.TotalDuration * 2f : definition.Active;
                    awaitingAnimation = animationDriven;
                    OnActiveBegin();
                    break;

                case AttackPhase.Recovery:
                    phaseTimer = animationDriven ? definition.TotalDuration * 2f : definition.Recovery;
                    awaitingAnimation = animationDriven;
                    break;
            }
        }

        private void Complete()
        {
            phase = AttackPhase.Ready;
            awaitingAnimation = false;
            StartCooldown();
            OnAttackComplete();
            Finished?.Invoke(this);
        }

        private void StartCooldown() => cooldownTimer = definition != null ? definition.Cooldown : 0f;

        private void ApplyMotion()
        {
            if (definition.Motion == AttackMotion.None || Owner.Movement == null)
            {
                return;
            }

            Vector2 direction = currentTarget != null
                ? (currentTarget.AimPosition - (Vector2)transform.position).normalized
                : new Vector2(Owner.FacingDirection, 0f);

            switch (definition.Motion)
            {
                case AttackMotion.Lunge:
                case AttackMotion.Dash:
                    Owner.Movement.TryDash(new Vector2(direction.x, 0f), definition.MotionForce, definition.MotionDuration);
                    break;

                case AttackMotion.Leap:
                    Owner.Movement.TryDash(new Vector2(direction.x, Mathf.Max(0.5f, direction.y)), definition.MotionForce, definition.MotionDuration);
                    break;
            }
        }

        /// <summary>
        /// Mirrors an attachment's local X to match the owner's facing, and returns its
        /// world position.
        /// <para>
        /// Flipping a SpriteRenderer does not move child objects, so a muzzle or a melee
        /// hitbox authored on the right stays on the right when the enemy turns left. Every
        /// attack runs its attachments through this, so an enemy attacks the way it faces
        /// without the designer having to wire anything up.
        /// </para>
        /// </summary>
        protected Vector2 ResolveAttachment(Transform attachment)
        {
            if (attachment == null)
            {
                return transform.position;
            }

            // ScaleX facing already mirrors children; mirroring again would undo it.
            if (Owner == null || Owner.FacingMovesChildren)
            {
                return attachment.position;
            }

            Vector3 local = attachment.localPosition;
            float wanted = Mathf.Abs(local.x) * Owner.FacingDirection;

            if (!Mathf.Approximately(local.x, wanted))
            {
                local.x = wanted;
                attachment.localPosition = local;
            }

            return attachment.position;
        }

        /// <summary>Builds the damage payload for this swing from its definition.</summary>
        protected DamageInfo BuildDamageInfo()
        {
            DamageInfo info = DamageInfo.Create(definition.Damage, Owner.Team, transform.position, Owner.gameObject);
            info.PoiseDamage = definition.PoiseDamage;
            info.Type = definition.DamageType;
            info.Flags = definition.DamageFlags;
            info.KnockbackForce = definition.KnockbackForce;
            info.AttackId = attackId;
            return info;
        }

        // --- Animation-event entry points, forwarded by AttackController. ---

        internal void AnimationHitboxOn()
        {
            if (phase == AttackPhase.Startup)
            {
                EnterPhase(AttackPhase.Active);
            }
        }

        internal void AnimationHitboxOff()
        {
            if (phase == AttackPhase.Active)
            {
                EnterPhase(AttackPhase.Recovery);
            }
        }

        internal void AnimationComplete()
        {
            if (IsRunning)
            {
                Complete();
            }
        }

        /// <summary>Animation asked for the projectile now. Only meaningful for ranged attacks.</summary>
        internal virtual void AnimationSpawnProjectile() { }

        // --- Subclass hooks. ---

        protected virtual void OnAttackBegin() { }

        /// <summary>The damaging part starts: enable a hitbox, fire a projectile.</summary>
        protected abstract void OnActiveBegin();

        /// <summary>The damaging part ends: disable the hitbox.</summary>
        protected abstract void OnActiveEnd();

        protected virtual void OnAttackComplete() { }

        protected virtual void OnAttackCancelled() { }
    }
}
