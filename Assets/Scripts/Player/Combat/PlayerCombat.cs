using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>Phases of a running player attack.</summary>
    public enum PlayerAttackPhase
    {
        None = 0,
        Startup,
        Active,
        Recovery,
    }

    /// <summary>
    /// Selects and runs the player's attacks.
    /// <para>
    /// It holds a list of <see cref="PlayerAttackDefinition"/> assets and answers one
    /// question: given an attack direction and the current movement context, which of these
    /// applies? Everything else — timing, hitbox, damage, combo, recoil — comes from the
    /// chosen asset. Adding attacks means adding assets to the list.
    /// </para>
    /// </summary>
    public class PlayerCombat : PlayerModule
    {
        [Header("Attacks")]
        [SerializeField, Tooltip("Every attack this player can perform. Selection matches on direction, movement context and combo position.")]
        private PlayerAttackDefinition[] attacks = Array.Empty<PlayerAttackDefinition>();

        [Header("Pacing")]
        [SerializeField, Min(0f), Tooltip("Minimum seconds between any two attacks, on top of each attack's own cooldown.")]
        private float globalCooldown;

        [Header("Debug")]
        [SerializeField, Tooltip("Log every attack selection and phase change.")]
        private bool logAttacks;

        /// <summary>Raised when an attack begins, for VFX, audio and UI.</summary>
        public event Action<PlayerAttackDefinition> AttackStarted;

        /// <summary>Raised when an attack finishes or is cancelled.</summary>
        public event Action<PlayerAttackDefinition> AttackFinished;

        /// <summary>Raised when an attack connects with something.</summary>
        public event Action<PlayerAttackDefinition, Hurtbox, DamageResult> AttackHit;

        private readonly Dictionary<PlayerAttackDefinition, float> cooldowns = new Dictionary<PlayerAttackDefinition, float>();

        private PlayerInputRouter input;
        private HitboxDirectory hitboxes;
        private ResourcePool resources;

        private PlayerAttackDefinition current;
        private Hitbox activeHitbox;
        private PlayerAttackPhase phase;
        private float phaseTimer;
        private float globalCooldownTimer;
        private float comboWindowTimer;
        private PlayerAttackDefinition comboNext;
        private bool movementLocked;
        private int attackIdCounter;
        private int currentAttackId;

        public override int TickOrder => PlayerTickOrder.Combat;

        public bool IsAttacking => phase != PlayerAttackPhase.None;

        public PlayerAttackPhase Phase => phase;

        public PlayerAttackDefinition CurrentAttack => current;

        /// <summary>True while a follow-up attack may still be queued.</summary>
        public bool IsComboWindowOpen => comboWindowTimer > 0f && comboNext != null;

        protected override void OnBind()
        {
            input = GetComponent<PlayerInputRouter>();
            hitboxes = GetComponent<HitboxDirectory>();
            resources = GetComponent<ResourcePool>();
        }

        public override void OnPlayerSpawned() => CancelAttack();

        public override void OnPlayerDied() => CancelAttack();

        public override void Tick(float deltaTime)
        {
            TickTimers(deltaTime);

            if (phase != PlayerAttackPhase.None)
            {
                TickAttack(deltaTime);
            }

            // A queued attack is consumed as soon as the player can act on it, which is
            // what makes buffered input feel immediate rather than dropped.
            if (input != null && CanStartAttack() && input.TryConsumeAttack(out AttackIntent intent))
            {
                TryAttack(intent.Direction);
            }
        }

        private void TickTimers(float deltaTime)
        {
            if (globalCooldownTimer > 0f)
            {
                globalCooldownTimer -= deltaTime;
            }

            if (comboWindowTimer > 0f)
            {
                comboWindowTimer -= deltaTime;
                if (comboWindowTimer <= 0f)
                {
                    comboNext = null;
                }
            }

            if (cooldowns.Count == 0)
            {
                return;
            }

            // Small dictionary (one entry per attack with a cooldown); allocation-free walk.
            tickBuffer.Clear();
            foreach (KeyValuePair<PlayerAttackDefinition, float> pair in cooldowns)
            {
                if (pair.Value > 0f)
                {
                    tickBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < tickBuffer.Count; i++)
            {
                cooldowns[tickBuffer[i]] = Mathf.Max(0f, cooldowns[tickBuffer[i]] - deltaTime);
            }
        }

        private readonly List<PlayerAttackDefinition> tickBuffer = new List<PlayerAttackDefinition>(8);

        private void TickAttack(float deltaTime)
        {
            phaseTimer -= deltaTime;
            if (phaseTimer > 0f)
            {
                return;
            }

            switch (phase)
            {
                case PlayerAttackPhase.Startup:
                    EnterPhase(PlayerAttackPhase.Active);
                    break;

                case PlayerAttackPhase.Active:
                    EnterPhase(PlayerAttackPhase.Recovery);
                    break;

                case PlayerAttackPhase.Recovery:
                    CompleteAttack();
                    break;
            }
        }

        /// <summary>True when the player is free to begin a new attack right now.</summary>
        public bool CanStartAttack()
        {
            if (globalCooldownTimer > 0f)
            {
                return false;
            }

            if (Owner.Motion != null && Owner.Motion.IsInCommittedMove)
            {
                return false;
            }

            if (phase == PlayerAttackPhase.None)
            {
                return true;
            }

            // Mid-attack, only a definition that allows cancelling into another attack lets
            // the next one start, and only once the active frames are over.
            return current != null
                   && (current.CancelRules & AttackCancel.IntoAttack) != 0
                   && phase == PlayerAttackPhase.Recovery;
        }

        /// <summary>
        /// Attempts an attack in <paramref name="direction"/>. Public so abilities, tools and
        /// scripted sequences can trigger attacks without going through input.
        /// </summary>
        public bool TryAttack(AttackDirection direction)
        {
            PlayerAttackDefinition definition = SelectAttack(direction);
            if (definition == null)
            {
                if (logAttacks)
                {
                    Debug.Log($"[PlayerCombat] no attack matched direction {direction} in context {CurrentContext()}", this);
                }

                return false;
            }

            return Begin(definition);
        }

        /// <summary>
        /// Chooses the attack that answers this direction in the current context.
        /// <para>
        /// A queued combo follow-up wins over a fresh opener, which is what turns repeated
        /// presses into a chain rather than the same first swing.
        /// </para>
        /// </summary>
        private PlayerAttackDefinition SelectAttack(AttackDirection direction)
        {
            AttackContext context = CurrentContext();

            if (comboNext != null && comboWindowTimer > 0f && Matches(comboNext, direction, context))
            {
                return comboNext;
            }

            PlayerAttackDefinition best = null;

            for (int i = 0; i < attacks.Length; i++)
            {
                PlayerAttackDefinition candidate = attacks[i];
                if (candidate == null || candidate.ComboIndex > 0)
                {
                    // Follow-ups are only reachable through a chain, never selected cold.
                    continue;
                }

                if (!Matches(candidate, direction, context))
                {
                    continue;
                }

                if (best == null || candidate.Priority > best.Priority)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private bool Matches(PlayerAttackDefinition definition, AttackDirection direction, AttackContext context)
        {
            if (definition.Direction != direction || !definition.IsAllowedIn(context))
            {
                return false;
            }

            if (cooldowns.TryGetValue(definition, out float remaining) && remaining > 0f)
            {
                return false;
            }

            return CanAfford(definition);
        }

        private bool CanAfford(PlayerAttackDefinition definition)
        {
            return definition.Cost.Resource == null || resources == null || resources.CanAfford(definition.Cost);
        }

        /// <summary>The player's movement context, as attack selection sees it.</summary>
        public AttackContext CurrentContext()
        {
            IPlayerMotionContext motion = Owner.Motion;
            if (motion == null)
            {
                return AttackContext.Grounded;
            }

            if (motion.IsWallSliding)
            {
                return AttackContext.OnWall;
            }

            return motion.IsGrounded ? AttackContext.Grounded : AttackContext.Airborne;
        }

        private bool Begin(PlayerAttackDefinition definition)
        {
            if (definition.Cost.Resource != null && resources != null && !resources.TrySpend(definition.Cost))
            {
                return false;
            }

            if (phase != PlayerAttackPhase.None)
            {
                CancelAttack();
            }

            current = definition;
            currentAttackId = ++attackIdCounter;
            comboNext = null;
            comboWindowTimer = 0f;
            globalCooldownTimer = globalCooldown;
            cooldowns[definition] = definition.Cooldown;

            if (definition.LockMovement)
            {
                Owner.PushMovementLock();
                movementLocked = true;
            }

            Owner.Animation.PlayAction(definition.AnimationKey);
            EnterPhase(PlayerAttackPhase.Startup);
            AttackStarted?.Invoke(definition);

            if (logAttacks)
            {
                Debug.Log($"[PlayerCombat] {definition.DisplayName} ({definition.Direction}) in {CurrentContext()}", this);
            }

            return true;
        }

        private void EnterPhase(PlayerAttackPhase next)
        {
            if (phase == PlayerAttackPhase.Active && next != PlayerAttackPhase.Active)
            {
                CloseHitbox();
            }

            phase = next;

            switch (next)
            {
                case PlayerAttackPhase.Startup:
                    phaseTimer = current.Startup;
                    break;

                case PlayerAttackPhase.Active:
                    phaseTimer = current.Active;
                    OpenHitbox();
                    ApplySelfVelocity();
                    break;

                case PlayerAttackPhase.Recovery:
                    phaseTimer = current.Recovery;
                    OpenComboWindow();
                    break;
            }
        }

        private void OpenHitbox()
        {
            if (hitboxes == null)
            {
                return;
            }

            activeHitbox = hitboxes.Resolve(current.HitboxKey);
            if (activeHitbox == null)
            {
                Debug.LogWarning($"Attack '{current.DisplayName}' wants hitbox '{current.HitboxKey}', which is not in the HitboxDirectory. It will deal no damage.", this);
                return;
            }

            activeHitbox.TargetTeams = Owner.HostileTeams;
            activeHitbox.Hit += OnHitboxHit;

            DamageInfo info = current.Payload.ToDamageInfo(Owner.Team, transform.position, gameObject, currentAttackId);
            activeHitbox.Activate(in info);
        }

        private void CloseHitbox()
        {
            if (activeHitbox == null)
            {
                return;
            }

            activeHitbox.Hit -= OnHitboxHit;
            activeHitbox.Deactivate();
            activeHitbox = null;
        }

        private void OnHitboxHit(Hurtbox victim, DamageResult result)
        {
            if (!result.Applied && !result.Blocked)
            {
                return;
            }

            HitStop.Request(current.Payload.HitStopSeconds);
            ApplyRecoil();
            AttackHit?.Invoke(current, victim, result);
        }

        /// <summary>
        /// The pogo bounce: a connecting downward air attack throws the player back up.
        /// Expressed as data on the attack rather than a special case for one move.
        /// </summary>
        private void ApplyRecoil()
        {
            if (current.RecoilOnHit <= 0f || Owner.Motion == null)
            {
                return;
            }

            if (current.RecoilRequiresAirborne && Owner.Motion.IsGrounded)
            {
                return;
            }

            Vector2 velocity = Owner.Motion.Velocity;
            Owner.Motion.SetVelocity(new Vector2(velocity.x, current.RecoilOnHit));
        }

        private void ApplySelfVelocity()
        {
            if (current.SelfVelocity == Vector2.zero || Owner.Motion == null)
            {
                return;
            }

            Vector2 v = current.SelfVelocity;
            v.x *= Owner.FacingDirection;
            Owner.Motion.SetVelocity(v);
        }

        private void OpenComboWindow()
        {
            if (current.FollowUp == null || current.ComboWindow <= 0f)
            {
                return;
            }

            comboNext = current.FollowUp;
            comboWindowTimer = current.ComboWindow;
        }

        private void CompleteAttack()
        {
            PlayerAttackDefinition finished = current;
            EndAttack();
            AttackFinished?.Invoke(finished);
        }

        /// <summary>Aborts the running attack and makes safe. Used on death, stagger and hard cancels.</summary>
        public void CancelAttack()
        {
            if (phase == PlayerAttackPhase.None)
            {
                return;
            }

            PlayerAttackDefinition cancelled = current;
            EndAttack();
            AttackFinished?.Invoke(cancelled);
        }

        private void EndAttack()
        {
            CloseHitbox();

            if (movementLocked)
            {
                Owner.PopMovementLock();
                movementLocked = false;
            }

            phase = PlayerAttackPhase.None;
            phaseTimer = 0f;
            current = null;
        }

        /// <summary>True when the current attack permits the given action.</summary>
        public bool Allows(AttackCancel action)
        {
            return phase == PlayerAttackPhase.None || current == null || (current.CancelRules & action) != 0;
        }
    }
}
