using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;
using DungeonSong.Combat.Pooling;

namespace DungeonSong.Enemies
{
    /// <summary>How an enemy's visuals are turned around.</summary>
    public enum FacingMode
    {
        /// <summary>Flip the SpriteRenderer. Correct for most 2D sprites.</summary>
        SpriteFlipX = 0,

        /// <summary>Negate local scale X. Use when children must flip too.</summary>
        ScaleX,

        /// <summary>The movement component handles facing itself (surface crawlers).</summary>
        None,
    }

    /// <summary>
    /// The hub of one enemy. It owns the lifecycle, the shared state every enemy has
    /// (team, facing, current target, health), and the tick loop that drives its modules.
    /// <para>
    /// What it deliberately does not own: how to move, how to see, how to decide, how to
    /// attack, how to defend. Those are modules, which is why adding the hundredth enemy
    /// never means editing this class.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class Enemy : MonoBehaviour, IEnemy, IPoolable
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Shared configuration: stats, movement and perception tuning.")]
        private EnemyDefinition definition;

        [SerializeField, Tooltip("Team this enemy fights for. Hits from its own team are ignored.")]
        private DamageTeam team = DamageTeam.Enemy;

        [SerializeField, Tooltip("Teams this enemy treats as enemies when acquiring targets.")]
        private DamageTeam hostileTeams = DamageTeam.Player;

        [Header("Lifecycle")]
        [SerializeField, Tooltip("Wire modules in Awake. Turn off only when a spawner initializes this enemy explicitly.")]
        private bool initializeOnAwake = true;

        [SerializeField, Tooltip("Start simulating as soon as it spawns. Turn off for ambushers that an EnemyActivation or trigger wakes up.")]
        private bool activateOnSpawn = true;

        [SerializeField, Tooltip("Return to the pool on death instead of being destroyed.")]
        private bool pooled;

        [Header("Facing")]
        [SerializeField] private FacingMode facingMode = FacingMode.SpriteFlipX;

        [SerializeField, Tooltip("Sprite to flip. Leave empty to search children.")]
        private SpriteRenderer spriteRenderer;

        [SerializeField, Tooltip("Direction the artwork faces when unflipped.")]
        private int artworkFacing = 1;

        [SerializeField, Tooltip("Facing applied on spawn. +1 right, -1 left.")]
        private int initialFacing = 1;

        // --- Events. One place for other systems to listen, so nothing has to reach into modules. ---

        /// <summary>Entered play, including reuse from a pool.</summary>
        public event Action<Enemy> Spawned;

        /// <summary>Began simulating.</summary>
        public event Action<Enemy> Activated;

        /// <summary>Stopped simulating but is still in the world.</summary>
        public event Action<Enemy> Deactivated;

        public event Action<Enemy, ITargetable> TargetAcquired;

        public event Action<Enemy, ITargetable> TargetLost;

        /// <summary>Took a hit. Fires for blocked and immune hits too; check the result.</summary>
        public event Action<Enemy, DamageInfo, DamageResult> Damaged;

        /// <summary>Lost its poise to a hit.</summary>
        public event Action<Enemy, DamageInfo> Staggered;

        public event Action<Enemy> Died;

        /// <summary>Left play. Anything holding a reference should drop it here.</summary>
        public event Action<Enemy> Despawned;

        /// <summary>An animation clip reported a footstep.</summary>
        public event Action<Enemy> Footstep;

        private readonly List<EnemyModule> modules = new List<EnemyModule>(8);
        private readonly List<EnemyModule> tickables = new List<EnemyModule>(8);

        private int facingDirection = 1;
        private bool initialized;
        private float despawnTimer;
        private bool despawnScheduled;

        // --- Cached roles. Resolved once during initialization; never looked up per frame. ---

        public EnemyDefinition Definition => definition;

        public Rigidbody2D Body { get; private set; }

        public EnemyHealth Health { get; private set; }

        public IMovementController Movement { get; private set; }

        public IKnockbackReceiver KnockbackReceiver { get; private set; }

        public IPerception Perception { get; private set; }

        public EnemyStateMachine Brain { get; private set; }

        public AttackController Attacks { get; private set; }

        public DefenseController Defense { get; private set; }

        /// <summary>Never null: falls back to <see cref="NullAnimatorAdapter"/>.</summary>
        public IAnimatorAdapter Animation { get; private set; } = NullAnimatorAdapter.Instance;

        public Transform Transform => transform;

        public DamageTeam Team => team;

        /// <summary>Teams this enemy will hunt.</summary>
        public DamageTeam HostileTeams => hostileTeams;

        public EnemyLifecycleState Lifecycle { get; private set; } = EnemyLifecycleState.Uninitialized;

        public bool IsAlive => Health == null || Health.IsAlive;

        public bool IsActive => Lifecycle == EnemyLifecycleState.Active;

        public int FacingDirection => facingDirection;

        /// <summary>
        /// True when turning around already mirrors child transforms (ScaleX mode).
        /// Sprite flipping does not move children, so attachments such as muzzles and
        /// melee hitboxes have to mirror themselves; this tells them whether to.
        /// </summary>
        public bool FacingMovesChildren => facingMode == FacingMode.ScaleX;

        /// <summary>Current target, from perception unless something forced one.</summary>
        public ITargetable CurrentTarget => Perception?.CurrentTarget;

        public bool HasTarget => CurrentTarget != null;

        /// <summary>Where the target was last seen. Valid after a target is lost.</summary>
        public Vector2 LastKnownTargetPosition => Perception?.LastKnownPosition ?? (Vector2)transform.position;

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void Start()
        {
            // Spawn is separate from Initialize so pooled and scene-placed enemies follow
            // the same path. Scene-placed ones spawn themselves here.
            if (Lifecycle == EnemyLifecycleState.Uninitialized || Lifecycle == EnemyLifecycleState.Dormant)
            {
                if (!despawnScheduled)
                {
                    Spawn();
                }
            }
        }

        /// <summary>
        /// Discovers and wires every module. Idempotent, and callable directly from tests
        /// or a spawner that needs the enemy configured before it enters play.
        /// </summary>
        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            Body = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // Role lookups happen exactly once. Everything afterwards uses these fields.
            Health = GetComponent<EnemyHealth>();
            Movement = GetComponent<IMovementController>();
            KnockbackReceiver = GetComponent<IKnockbackReceiver>();
            Perception = GetComponent<IPerception>();
            Brain = GetComponent<EnemyStateMachine>();
            Attacks = GetComponent<AttackController>();
            Defense = GetComponent<DefenseController>();

            var adapter = GetComponent<IAnimatorAdapter>();
            Animation = adapter ?? (IAnimatorAdapter)NullAnimatorAdapter.Instance;

            GetComponents(modules);
            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].Bind(this);
            }

            modules.Sort(CompareTickOrder);

            tickables.Clear();
            for (int i = 0; i < modules.Count; i++)
            {
                // States are ticked by the state machine, not by the enemy.
                if (modules[i] is not IEnemyState)
                {
                    tickables.Add(modules[i]);
                }
            }

            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].OnEnemyInitialized();
            }

            SubscribeToHealth();
            SubscribeToPerception();

            Lifecycle = EnemyLifecycleState.Dormant;
            SetFacing(initialFacing);
        }

        /// <summary>Puts the enemy into play at its current position.</summary>
        public void Spawn() => Spawn(transform.position, initialFacing);

        /// <summary>Puts the enemy into play at a position and facing.</summary>
        public void Spawn(Vector2 position, int facing)
        {
            Initialize();

            transform.position = position;
            despawnScheduled = false;
            despawnTimer = 0f;
            SetFacing(facing);

            Lifecycle = EnemyLifecycleState.Dormant;

            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].OnEnemySpawned();
            }

            Spawned?.Invoke(this);

            if (activateOnSpawn)
            {
                Activate();
            }
        }

        public void Activate()
        {
            if (Lifecycle == EnemyLifecycleState.Active || Lifecycle == EnemyLifecycleState.Dead)
            {
                return;
            }

            Lifecycle = EnemyLifecycleState.Active;
            Activated?.Invoke(this);
        }

        public void Deactivate()
        {
            if (Lifecycle != EnemyLifecycleState.Active)
            {
                return;
            }

            Movement?.Stop(true);
            Lifecycle = EnemyLifecycleState.Dormant;
            Deactivated?.Invoke(this);
        }

        public void Kill() => Health?.Kill();

        /// <summary>
        /// Removes the enemy from play. Pooled enemies go back to the pool; others are
        /// destroyed. Either way <see cref="Despawned"/> fires first.
        /// </summary>
        public void Despawn()
        {
            if (Lifecycle == EnemyLifecycleState.Despawned)
            {
                return;
            }

            Lifecycle = EnemyLifecycleState.Despawned;

            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].OnEnemyDespawned();
            }

            Despawned?.Invoke(this);

            if (pooled)
            {
                PrefabPool.Release(gameObject);
            }
            else if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                // Edit mode (tests, editor tooling) rejects Destroy.
                DestroyImmediate(gameObject);
            }
        }

        /// <summary>Schedules a despawn, letting a death animation finish first.</summary>
        public void DespawnAfter(float seconds)
        {
            if (seconds <= 0f)
            {
                Despawn();
                return;
            }

            despawnScheduled = true;
            despawnTimer = seconds;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            float dt = Time.deltaTime;

            if (despawnScheduled)
            {
                despawnTimer -= dt;
                if (despawnTimer <= 0f)
                {
                    Despawn();
                    return;
                }
            }

            bool dormant = Lifecycle == EnemyLifecycleState.Dormant;
            if (Lifecycle == EnemyLifecycleState.Despawned || Lifecycle == EnemyLifecycleState.Uninitialized)
            {
                return;
            }

            for (int i = 0; i < tickables.Count; i++)
            {
                EnemyModule module = tickables[i];
                if (dormant && !module.TickWhileDormant)
                {
                    continue;
                }

                if (module.enabled)
                {
                    module.Tick(dt);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!initialized || Lifecycle == EnemyLifecycleState.Despawned)
            {
                return;
            }

            bool dormant = Lifecycle == EnemyLifecycleState.Dormant;
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < tickables.Count; i++)
            {
                EnemyModule module = tickables[i];
                if (dormant && !module.TickWhileDormant)
                {
                    continue;
                }

                if (module.enabled)
                {
                    module.FixedTick(dt);
                }
            }
        }

        /// <summary>Turns the enemy to face <paramref name="sign"/> (+1 right, -1 left).</summary>
        public void SetFacing(int sign)
        {
            if (sign == 0)
            {
                return;
            }

            facingDirection = sign > 0 ? 1 : -1;
            ApplyFacingVisual();
        }

        /// <summary>Turns the enemy toward a world position.</summary>
        public void FaceTowards(Vector2 worldPosition)
        {
            float dx = worldPosition.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f)
            {
                SetFacing(dx > 0f ? 1 : -1);
            }
        }

        /// <summary>Turns the enemy toward its current target, if it has one.</summary>
        public void FaceTarget()
        {
            ITargetable target = CurrentTarget;
            if (target != null)
            {
                FaceTowards(target.AimPosition);
            }
        }

        private void ApplyFacingVisual()
        {
            switch (facingMode)
            {
                case FacingMode.SpriteFlipX:
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.flipX = artworkFacing > 0 ? facingDirection < 0 : facingDirection > 0;
                    }

                    break;

                case FacingMode.ScaleX:
                    Vector3 scale = transform.localScale;
                    float magnitude = Mathf.Abs(scale.x);
                    scale.x = artworkFacing > 0 ? magnitude * facingDirection : -magnitude * facingDirection;
                    transform.localScale = scale;
                    break;

                case FacingMode.None:
                    break;
            }
        }

        /// <summary>Called by <see cref="AnimationEventRelay"/>.</summary>
        public void RaiseFootstep() => Footstep?.Invoke(this);

        /// <summary>Finds a module by type. Intended for setup and editor tooling, not hot paths.</summary>
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

        private void SubscribeToHealth()
        {
            if (Health == null)
            {
                return;
            }

            Health.Damaged += OnHealthDamaged;
            Health.Staggered += OnHealthStaggered;
            Health.Died += OnHealthDied;
        }

        private void SubscribeToPerception()
        {
            if (Perception == null)
            {
                return;
            }

            Perception.TargetAcquired += OnPerceptionTargetAcquired;
            Perception.TargetLost += OnPerceptionTargetLost;
        }

        private void OnDestroy()
        {
            if (Health != null)
            {
                Health.Damaged -= OnHealthDamaged;
                Health.Staggered -= OnHealthStaggered;
                Health.Died -= OnHealthDied;
            }

            if (Perception != null)
            {
                Perception.TargetAcquired -= OnPerceptionTargetAcquired;
                Perception.TargetLost -= OnPerceptionTargetLost;
            }
        }

        private void OnHealthDamaged(DamageInfo info, DamageResult result) => Damaged?.Invoke(this, info, result);

        private void OnHealthStaggered(DamageInfo info) => Staggered?.Invoke(this, info);

        private void OnPerceptionTargetAcquired(ITargetable target) => TargetAcquired?.Invoke(this, target);

        private void OnPerceptionTargetLost(ITargetable target) => TargetLost?.Invoke(this, target);

        private void OnHealthDied(DamageInfo info)
        {
            Lifecycle = EnemyLifecycleState.Dead;
            Movement?.Stop(true);

            for (int i = 0; i < modules.Count; i++)
            {
                modules[i].OnEnemyDied();
            }

            Died?.Invoke(this);

            // A DeadState, if present, owns the death sequence and the despawn timing.
            // Without one the enemy still cleans itself up after the configured delay.
            if (Brain == null || !Brain.HasDeathState)
            {
                float delay = Health != null && Health.Stats != null ? Health.Stats.DespawnDelay : 0f;
                DespawnAfter(delay);
            }
        }

        public void OnSpawnedFromPool()
        {
            Lifecycle = EnemyLifecycleState.Dormant;
            despawnScheduled = false;
        }

        public void OnReturnedToPool()
        {
            Lifecycle = EnemyLifecycleState.Despawned;
            despawnScheduled = false;
        }

        private static int CompareTickOrder(EnemyModule a, EnemyModule b) => a.TickOrder.CompareTo(b.TickOrder);
    }
}
