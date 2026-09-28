using UnityEngine;
using DungeonSong.Combat.Pooling;

namespace DungeonSong.Combat.Projectiles
{
    /// <summary>
    /// A pooled projectile. Motion comes from its <see cref="ProjectileDefinition"/>;
    /// damage is delegated to a <see cref="Hitbox"/> on the same object so projectiles
    /// and melee swings deal damage through exactly one code path.
    /// Override <see cref="UpdateMotion"/> for exotic flight paths.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Projectile : MonoBehaviour, IPoolable
    {
        [Header("Wiring")]
        [SerializeField, Tooltip("Hitbox that deals this projectile's damage. Leave empty to search children.")]
        private Hitbox hitbox;

        [SerializeField, Tooltip("Layers treated as terrain for impact tests.")]
        private LayerMask terrainMask = 1 << 8;

        [SerializeField, Tooltip("Pass through trigger colliders. Leave on: sensors, hurtboxes, room gates and pickups are all triggers sitting on ordinary layers, and a projectile must not die on them.")]
        private bool ignoreTriggerColliders = true;

        [Header("Presentation")]
        [SerializeField, Tooltip("Rotate the sprite to face the direction of travel.")]
        private bool alignToVelocity = true;

        private Rigidbody2D body;
        private ProjectileDefinition definition;
        private ITargetable homingTarget;
        private GameObject attacker;
        private Vector2 launchOrigin;
        private Vector2 direction;
        private float lifeTimer;
        private int hitsRemaining;
        private bool returning;
        private bool active;

        /// <summary>Raised when the projectile stops existing, for VFX and audio.</summary>
        public event System.Action<Projectile, Vector2> Despawned;

        protected ProjectileDefinition Definition => definition;

        protected Rigidbody2D Body => body;

        protected Vector2 Direction => direction;

        private bool wired;

        private void Awake() => EnsureWired();

        /// <summary>
        /// Resolves the body and hitbox exactly once. Called from Awake, and again from
        /// <see cref="Launch"/> so a projectile is usable even when Awake has not run —
        /// which is the case in edit-mode tests.
        /// </summary>
        private void EnsureWired()
        {
            if (wired)
            {
                return;
            }

            wired = true;
            body = GetComponent<Rigidbody2D>();

            if (hitbox == null)
            {
                hitbox = GetComponentInChildren<Hitbox>();
            }

            if (hitbox != null)
            {
                hitbox.Hit += OnHitboxHit;
            }
        }

        private void OnDestroy()
        {
            if (hitbox != null)
            {
                hitbox.Hit -= OnHitboxHit;
            }
        }

        /// <summary>
        /// Arms the projectile. Called by <see cref="ProjectileSpawner"/> right after it
        /// leaves the pool.
        /// </summary>
        public void Launch(ProjectileDefinition def, Vector2 aimDirection, DamageTeam sourceTeam, DamageTeam targetTeams, GameObject source, ITargetable target = null)
        {
            EnsureWired();

            definition = def;
            attacker = source;
            homingTarget = target;
            direction = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
            launchOrigin = transform.position;
            lifeTimer = def.Lifetime;
            hitsRemaining = def.PierceCount + 1;
            returning = false;
            active = true;

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = def.Motion == ProjectileMotion.Arcing ? def.GravityScale : 0f;
            body.linearVelocity = direction * def.Speed;

            if (hitbox != null)
            {
                hitbox.TargetTeams = targetTeams;

                DamageInfo info = DamageInfo.Create(def.Damage, sourceTeam, transform.position, source);
                info.Type = def.DamageType;
                info.PoiseDamage = def.PoiseDamage;
                info.KnockbackForce = def.KnockbackForce;
                hitbox.Activate(in info);
            }

            ApplyFacing();
        }

        private void FixedUpdate()
        {
            if (!active)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            lifeTimer -= dt;
            if (lifeTimer <= 0f)
            {
                Despawn();
                return;
            }

            UpdateMotion(dt);
            ApplyFacing();
        }

        /// <summary>
        /// Per-step motion. The base implementation covers the four stock
        /// <see cref="ProjectileMotion"/> modes; override to add new flight behaviour
        /// without touching the damage or pooling paths.
        /// </summary>
        protected virtual void UpdateMotion(float dt)
        {
            switch (definition.Motion)
            {
                case ProjectileMotion.Straight:
                case ProjectileMotion.Arcing:
                    // Velocity was set at launch; gravity (if any) does the rest.
                    break;

                case ProjectileMotion.Homing:
                    SteerToward(homingTarget != null && homingTarget.IsValidTarget
                        ? homingTarget.AimPosition
                        : (Vector2)transform.position + direction, dt);
                    break;

                case ProjectileMotion.Returning:
                    UpdateReturning(dt);
                    break;
            }
        }

        private void UpdateReturning(float dt)
        {
            if (!returning)
            {
                float elapsed = definition.Lifetime - lifeTimer;
                if (elapsed >= definition.OutboundDuration)
                {
                    returning = true;
                }

                return;
            }

            Vector2 home = attacker != null ? (Vector2)attacker.transform.position : launchOrigin;
            SteerToward(home, dt);

            if (((Vector2)transform.position - home).sqrMagnitude < 0.25f)
            {
                Despawn();
            }
        }

        private void SteerToward(Vector2 worldPoint, float dt)
        {
            Vector2 desired = (worldPoint - (Vector2)transform.position).normalized;
            float maxRadians = definition.HomingTurnRate * Mathf.Deg2Rad * dt;
            direction = Vector3.RotateTowards(direction, desired, maxRadians, 0f);
            body.linearVelocity = direction * definition.Speed;
        }

        private void ApplyFacing()
        {
            if (!alignToVelocity)
            {
                return;
            }

            Vector2 v = body.linearVelocity;
            if (v.sqrMagnitude > 0.0001f)
            {
                transform.right = v;
            }
        }

        private void OnHitboxHit(Hurtbox hurtbox, DamageResult result)
        {
            if (!active || (!result.Applied && !result.Blocked))
            {
                return;
            }

            hitsRemaining--;
            if (hitsRemaining <= 0)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (ShouldDespawnOn(other))
            {
                Despawn();
            }
        }

        /// <summary>
        /// Whether hitting <paramref name="other"/> should end this projectile's flight.
        /// <para>
        /// Only solid terrain stops a projectile. Trigger colliders never do, because
        /// character sensors, hurtboxes, room gates and pickups all sit on ordinary
        /// layers: a projectile that died on those would vanish before its hitbox had
        /// resolved the hit, which reads in game as an attack that randomly does nothing.
        /// </para>
        /// </summary>
        public bool ShouldDespawnOn(Collider2D other)
        {
            if (!active || other == null || definition == null || !definition.DespawnOnTerrain)
            {
                return false;
            }

            if ((terrainMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return false;
            }

            if (ignoreTriggerColliders && other.isTrigger)
            {
                return false;
            }

            // Never die on something damageable: that hit belongs to the hitbox, and the
            // order of trigger callbacks within one physics step is not guaranteed.
            return other.GetComponentInParent<IDamageable>() == null;
        }

        /// <summary>Retires the projectile and returns it to the pool.</summary>
        public void Despawn()
        {
            if (!active)
            {
                return;
            }

            active = false;
            hitbox?.Deactivate();
            body.linearVelocity = Vector2.zero;
            Despawned?.Invoke(this, transform.position);
            PrefabPool.Release(gameObject);
        }

        public void OnSpawnedFromPool()
        {
            active = false;
            returning = false;
        }

        public void OnReturnedToPool()
        {
            active = false;
            homingTarget = null;
            attacker = null;
            hitbox?.Deactivate();
        }
    }
}
