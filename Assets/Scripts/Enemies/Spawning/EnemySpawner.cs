using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>When a spawner releases its enemies.</summary>
    public enum SpawnTrigger
    {
        /// <summary>As soon as the scene starts.</summary>
        OnStart = 0,

        /// <summary>When a hostile target comes within range.</summary>
        OnProximity,

        /// <summary>Only when something calls <see cref="EnemySpawner.SpawnAll"/>.</summary>
        Manual,
    }

    /// <summary>What happens after the spawned enemies die.</summary>
    public enum RespawnPolicy
    {
        /// <summary>Gone for the rest of the scene.</summary>
        Never = 0,

        /// <summary>Respawn after a delay, for corridors that should stay dangerous.</summary>
        AfterDelay,

        /// <summary>Respawn once the player leaves and returns.</summary>
        OnReturn,
    }

    /// <summary>
    /// Places enemies in the world from <see cref="EnemyDefinition"/> assets. Knows nothing
    /// about any specific enemy, so one spawner component covers every encounter in the game.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        /// <summary>One enemy to place, and where.</summary>
        [Serializable]
        public struct SpawnEntry
        {
            [Tooltip("Which enemy to spawn.")]
            public EnemyDefinition Definition;

            [Tooltip("Offset from this spawner's position.")]
            public Vector2 Offset;

            [Tooltip("Facing on spawn. +1 right, -1 left.")]
            public int Facing;

            [Min(1), Tooltip("How many to spawn at this point.")]
            public int Count;
        }

        [Header("What To Spawn")]
        [SerializeField] private SpawnEntry[] entries = Array.Empty<SpawnEntry>();

        [Header("When")]
        [SerializeField] private SpawnTrigger trigger = SpawnTrigger.OnStart;

        [SerializeField, Min(0f), Tooltip("Range for the proximity trigger.")]
        private float proximityRadius = 12f;

        [SerializeField, Tooltip("Teams that can set off the proximity trigger.")]
        private DamageTeam triggerTeams = DamageTeam.Player;

        [Header("Respawning")]
        [SerializeField] private RespawnPolicy respawn = RespawnPolicy.Never;

        [SerializeField, Min(0f), Tooltip("Delay for the AfterDelay policy.")]
        private float respawnDelay = 10f;

        [Header("Limits")]
        [SerializeField, Min(0), Tooltip("Maximum alive from this spawner at once. 0 = no limit.")]
        private int maxAlive;

        [Header("Parenting")]
        [SerializeField, Tooltip("Parent spawned enemies to this spawner. Off keeps the hierarchy flat, which is usually what you want.")]
        private bool parentToSpawner;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        /// <summary>Raised for each enemy spawned, so rooms and encounters can track them.</summary>
        public event Action<Enemy> EnemySpawned;

        /// <summary>Raised when every enemy from this spawner is dead.</summary>
        public event Action<EnemySpawner> Cleared;

        private readonly List<Enemy> alive = new List<Enemy>(8);
        private float respawnTimer;
        private bool hasSpawned;
        private bool playerLeft;

        /// <summary>Enemies from this spawner that are still alive.</summary>
        public IReadOnlyList<Enemy> Alive => alive;

        public bool IsCleared => hasSpawned && alive.Count == 0;

        private void Start()
        {
            if (trigger == SpawnTrigger.OnStart)
            {
                SpawnAll();
            }
        }

        private void Update()
        {
            if (trigger == SpawnTrigger.OnProximity && !hasSpawned && IsTargetNear())
            {
                SpawnAll();
            }

            HandleRespawn();
        }

        private bool IsTargetNear()
        {
            return TargetRegistry.FindNearest(transform.position, triggerTeams, proximityRadius) != null;
        }

        private void HandleRespawn()
        {
            if (!IsCleared || respawn == RespawnPolicy.Never)
            {
                return;
            }

            switch (respawn)
            {
                case RespawnPolicy.AfterDelay:
                    respawnTimer -= Time.deltaTime;
                    if (respawnTimer <= 0f)
                    {
                        hasSpawned = false;
                        SpawnAll();
                    }

                    break;

                case RespawnPolicy.OnReturn:
                    bool near = IsTargetNear();
                    if (!near)
                    {
                        playerLeft = true;
                    }
                    else if (playerLeft)
                    {
                        playerLeft = false;
                        hasSpawned = false;
                        SpawnAll();
                    }

                    break;
            }
        }

        /// <summary>Spawns every configured entry. Safe to call repeatedly; obeys the alive limit.</summary>
        public void SpawnAll()
        {
            hasSpawned = true;
            respawnTimer = respawnDelay;

            for (int i = 0; i < entries.Length; i++)
            {
                SpawnEntry entry = entries[i];
                int count = Mathf.Max(1, entry.Count);

                for (int n = 0; n < count; n++)
                {
                    if (maxAlive > 0 && alive.Count >= maxAlive)
                    {
                        return;
                    }

                    SpawnOne(entry);
                }
            }
        }

        /// <summary>Spawns a single entry, wiring up death tracking.</summary>
        public Enemy SpawnOne(SpawnEntry entry)
        {
            Vector2 position = (Vector2)transform.position + entry.Offset;
            int facing = entry.Facing == 0 ? 1 : entry.Facing;

            Enemy enemy = EnemyFactory.Spawn(entry.Definition, position, facing, parentToSpawner ? transform : null);
            if (enemy == null)
            {
                return null;
            }

            alive.Add(enemy);
            enemy.Despawned += OnEnemyDespawned;
            EnemySpawned?.Invoke(enemy);
            return enemy;
        }

        private void OnEnemyDespawned(Enemy enemy)
        {
            enemy.Despawned -= OnEnemyDespawned;
            alive.Remove(enemy);

            if (alive.Count == 0 && hasSpawned)
            {
                respawnTimer = respawnDelay;
                Cleared?.Invoke(this);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] != null)
                {
                    alive[i].Despawned -= OnEnemyDespawned;
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);

            for (int i = 0; i < entries.Length; i++)
            {
                Vector3 point = transform.position + (Vector3)entries[i].Offset;
                Gizmos.DrawWireCube(point, new Vector3(0.5f, 0.9f, 0f));
                Gizmos.DrawLine(transform.position, point);
            }

            if (trigger == SpawnTrigger.OnProximity)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
                Gizmos.DrawWireSphere(transform.position, proximityRadius);
            }
        }
    }
}
