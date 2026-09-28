using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Turns raw input into buffered player intent.
    /// <para>
    /// Two jobs, both of which action combat lives or dies on. First, it translates
    /// <em>input</em> ("up is held and the attack button went down") into <em>intent</em>
    /// ("attack upward"), so no combat code ever polls a key. Second, it buffers that intent
    /// for a short window, so an attack pressed slightly before the previous one finishes
    /// still comes out instead of being swallowed.
    /// </para>
    /// </summary>
    public class PlayerInputRouter : PlayerModule
    {
        [Header("Source")]
        [SerializeField, Tooltip("Where raw input comes from. Leave empty to search this object.")]
        private MonoBehaviour inputSourceComponent;

        [Header("Buffering")]
        [SerializeField, Min(0f), Tooltip("Seconds a pressed attack stays queued waiting for the player to become able to act. 0.15-0.2 feels responsive without firing ghost attacks.")]
        private float attackBufferSeconds = 0.18f;

        [SerializeField, Min(0f), Tooltip("Seconds an ability press stays queued.")]
        private float abilityBufferSeconds = 0.15f;

        [Header("Direction")]
        [SerializeField, Range(0.1f, 0.95f), Tooltip("How far the stick or key axis must be pushed to count as a directional attack.")]
        private float directionDeadzone = 0.5f;

        [SerializeField, Tooltip("Allow diagonal attack directions while airborne.")]
        private bool allowAirDiagonals = true;

        private IPlayerInputSource source;

        private AttackIntent bufferedAttack;
        private bool hasBufferedAttack;

        private AbilityIntent bufferedAbility;
        private bool hasBufferedAbility;

        public override int TickOrder => PlayerTickOrder.Input;

        /// <summary>Raw input, exposed for held/released checks by charge attacks.</summary>
        public IPlayerInputSource Source => source;

        /// <summary>True while an attack press is queued and still fresh.</summary>
        public bool HasBufferedAttack => hasBufferedAttack;

        protected override void OnBind()
        {
            source = inputSourceComponent as IPlayerInputSource;

            if (source == null)
            {
                source = GetComponent<IPlayerInputSource>();
            }

            if (source == null)
            {
                Debug.LogWarning($"PlayerInputRouter on '{name}' has no IPlayerInputSource; the player will not respond to input.", this);
            }
        }

        public override void Tick(float deltaTime)
        {
            if (source == null)
            {
                return;
            }

            ExpireBuffers();

            if (source.AttackPressed)
            {
                bufferedAttack = new AttackIntent(ResolveAttackDirection(), Time.time);
                hasBufferedAttack = true;
            }

            PollAbilities();
        }

        private void ExpireBuffers()
        {
            if (hasBufferedAttack && Time.time - bufferedAttack.TimeStamp > attackBufferSeconds)
            {
                hasBufferedAttack = false;
            }

            if (hasBufferedAbility && Time.time - bufferedAbility.TimeStamp > abilityBufferSeconds)
            {
                hasBufferedAbility = false;
            }
        }

        private void PollAbilities()
        {
            // Slot count is small and fixed; a loop here costs nothing and keeps the
            // binding table in one place.
            for (int slot = 0; slot < 8; slot++)
            {
                if (source.AbilityPressed(slot))
                {
                    bufferedAbility = new AbilityIntent(slot, Time.time);
                    hasBufferedAbility = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Reads the attack direction from movement input plus movement context. Wall
        /// context wins, because a wall attack is the only sensible thing to do there.
        /// </summary>
        private AttackDirection ResolveAttackDirection()
        {
            IPlayerMotionContext motion = Owner.Motion;
            Vector2 axis = source.MoveAxis;

            bool airborne = motion != null && !motion.IsGrounded;

            if (motion != null && motion.IsWallSliding)
            {
                return AttackDirection.Wall;
            }

            bool up = axis.y >= directionDeadzone;
            bool down = axis.y <= -directionDeadzone;
            bool forward = Mathf.Abs(axis.x) >= directionDeadzone;

            if (up)
            {
                return allowAirDiagonals && airborne && forward
                    ? AttackDirection.DiagonalUpForward
                    : AttackDirection.Up;
            }

            if (down)
            {
                // Down only means "pogo" in the air; grounded down-attacks are still useful
                // for low enemies, so both are allowed and the catalog decides.
                return allowAirDiagonals && airborne && forward
                    ? AttackDirection.DiagonalDownForward
                    : AttackDirection.Down;
            }

            return AttackDirection.Forward;
        }

        /// <summary>
        /// Takes the queued attack, if any. Consuming it clears the buffer, so one press
        /// can only ever produce one attack.
        /// </summary>
        public bool TryConsumeAttack(out AttackIntent intent)
        {
            intent = bufferedAttack;
            if (!hasBufferedAttack)
            {
                return false;
            }

            hasBufferedAttack = false;
            return true;
        }

        /// <summary>Takes the queued ability activation, if any.</summary>
        public bool TryConsumeAbility(out AbilityIntent intent)
        {
            intent = bufferedAbility;
            if (!hasBufferedAbility)
            {
                return false;
            }

            hasBufferedAbility = false;
            return true;
        }

        /// <summary>Drops queued intent, e.g. on death or when entering a rest state.</summary>
        public void ClearBuffers()
        {
            hasBufferedAttack = false;
            hasBufferedAbility = false;
        }

        public override void OnPlayerDied() => ClearBuffers();
    }
}
