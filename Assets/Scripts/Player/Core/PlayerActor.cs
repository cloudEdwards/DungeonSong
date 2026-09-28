using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>
    /// The player's hub. It owns the lifecycle and the module tick loop, and holds the
    /// cached references every module needs.
    /// <para>
    /// What it deliberately does not own: how to move, how to attack, which spells exist,
    /// what a campfire is. Those are modules and data. Adding the hundredth attack or the
    /// thirtieth spell must never mean editing this class or PlayerController.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerActor : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Team the player fights for. Hits from this team are ignored.")]
        private DamageTeam team = DamageTeam.Player;

        [SerializeField, Tooltip("Teams the player's attacks may damage.")]
        private DamageTeam hostileTeams = DamageTeam.Enemy;

        [Header("Lifecycle")]
        [SerializeField, Tooltip("Wire modules in Awake. Leave on unless something else initializes the player explicitly.")]
        private bool initializeOnAwake = true;

        // --- Events. One place for UI, audio and world systems to listen. ---

        /// <summary>Raised when the player enters play, including after a respawn.</summary>
        public event Action<PlayerActor> Spawned;

        /// <summary>Raised on any hit that reached the player, blocked or not.</summary>
        public event Action<PlayerActor, DamageInfo, DamageResult> Damaged;

        public event Action<PlayerActor> Died;

        /// <summary>Raised after the player respawns at a checkpoint.</summary>
        public event Action<PlayerActor> Respawned;

        private readonly List<PlayerModule> modules = new List<PlayerModule>(8);
        private int movementLockCount;
        private bool initialized;

        // --- Cached roles, resolved once. Never looked up per frame. ---

        public DamageTeam Team => team;

        /// <summary>Teams this player's attacks are allowed to damage.</summary>
        public DamageTeam HostileTeams => hostileTeams;

        /// <summary>Movement state and the movement lock. Implemented by PlayerController.</summary>
        public IPlayerMotionContext Motion { get; private set; }

        /// <summary>The player's health pool. Implemented by PlayerHealth.</summary>
        public IHealth Health { get; private set; }

        public Rigidbody2D Body { get; private set; }

        /// <summary>Never null: falls back to a no-op adapter when no animator is wired.</summary>
        public IPlayerAnimator Animation { get; private set; } = NullPlayerAnimator.Instance;

        public PlayerCombat Combat { get; private set; }

        public AbilityLoadout Abilities { get; private set; }

        public ResourcePool Resources { get; private set; }

        public bool IsAlive => Health == null || Health.IsAlive;

        /// <summary>+1 facing right, -1 facing left. Sourced from movement.</summary>
        public int FacingDirection => Motion?.FacingDirection ?? 1;

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void Start() => RaiseSpawned();

        /// <summary>
        /// Discovers and wires every module. Idempotent, and callable directly from tests.
        /// </summary>
        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            Body = GetComponent<Rigidbody2D>();

            // These interfaces are implemented by components in the predefined assembly
            // (PlayerController, PlayerHealth), which is why they are resolved by interface
            // rather than by concrete type: this assembly cannot reference those classes.
            Motion = GetComponent<IPlayerMotionContext>();
            Health = GetComponent<IHealth>();

            var animator = GetComponent<IPlayerAnimator>();
            Animation = animator ?? (IPlayerAnimator)NullPlayerAnimator.Instance;

            Combat = GetComponent<PlayerCombat>();
            Abilities = GetComponent<AbilityLoadout>();
            Resources = GetComponent<ResourcePool>();

            if (Motion == null)
            {
                Debug.LogWarning($"PlayerActor on '{name}' found no IPlayerMotionContext; attacks cannot read movement state.", this);
            }

            GetComponents(modules);
            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].Bind(this);
            }

            modules.Sort((a, b) => a.TickOrder.CompareTo(b.TickOrder));

            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].OnPlayerInitialized();
            }

            if (Health != null)
            {
                Health.Damaged += OnHealthDamaged;
                Health.Died += OnHealthDied;
            }
        }

        private void OnDestroy()
        {
            if (Health != null)
            {
                Health.Damaged -= OnHealthDamaged;
                Health.Died -= OnHealthDied;
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].enabled)
                {
                    modules[i].Tick(dt);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].enabled)
                {
                    modules[i].FixedTick(dt);
                }
            }
        }

        /// <summary>
        /// Requests that the player's own movement input be suppressed. Reference counted,
        /// so overlapping attacks and abilities cannot leave the player permanently rooted.
        /// </summary>
        public void PushMovementLock()
        {
            movementLockCount++;
            if (movementLockCount == 1)
            {
                Motion?.SetMovementLock(true);
            }
        }

        /// <summary>Releases one movement lock. Safe to over-call.</summary>
        public void PopMovementLock()
        {
            if (movementLockCount == 0)
            {
                return;
            }

            movementLockCount--;
            if (movementLockCount == 0)
            {
                Motion?.SetMovementLock(false);
            }
        }

        /// <summary>Clears every outstanding movement lock. Used on death and respawn.</summary>
        public void ClearMovementLocks()
        {
            movementLockCount = 0;
            Motion?.SetMovementLock(false);
        }

        /// <summary>Finds a module by type. For setup and tooling, not hot paths.</summary>
        public T GetModule<T>() where T : class
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is T match)
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>Places the player and restores it to a playable state.</summary>
        public void RespawnAt(Vector2 position, int facing)
        {
            ClearMovementLocks();
            transform.position = position;

            if (Body != null)
            {
                Body.linearVelocity = Vector2.zero;
            }

            Motion?.SetFacing(facing == 0 ? 1 : facing);
            Health?.RestoreToFull();

            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].OnPlayerSpawned();
            }

            Respawned?.Invoke(this);
            Spawned?.Invoke(this);
        }

        private void RaiseSpawned()
        {
            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].OnPlayerSpawned();
            }

            Spawned?.Invoke(this);
        }

        private void OnHealthDamaged(DamageInfo info, DamageResult result) => Damaged?.Invoke(this, info, result);

        private void OnHealthDied()
        {
            ClearMovementLocks();

            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].OnPlayerDied();
            }

            Died?.Invoke(this);
        }
    }
}
