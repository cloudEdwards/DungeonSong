using System;
using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Per-instance health, poise and invulnerability. This is the single place damage is
    /// applied, so every source (melee, projectile, contact, environment) goes through the
    /// same defense pipeline and raises the same events.
    /// <para>
    /// Runtime values live here, on the instance, and never in the shared
    /// <see cref="EnemyStats"/> asset.
    /// </para>
    /// </summary>
    public class EnemyHealth : EnemyModule, IDamageable, IHealth
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Overrides the stats on the Enemy's definition. Leave empty to use the definition.")]
        private EnemyStats statsOverride;

        [Header("Debug")]
        [SerializeField, Tooltip("Read-only view of current health while playing.")]
        private float debugCurrentHealth;

        private EnemyStats stats;
        private float current;
        private float max;
        private float poise;
        private float invulnerabilityTimer;
        private float poiseRegenDelayTimer;

        /// <summary>Raised for every hit that reached this component, blocked or not.</summary>
        public event Action<DamageInfo, DamageResult> Damaged;

        /// <summary>Raised when poise breaks. The brain turns this into a stagger state.</summary>
        public event Action<DamageInfo> Staggered;

        /// <summary>Raised once, when health reaches zero.</summary>
        public event Action<DamageInfo> Died;

        /// <summary>Raised on any change to current health, for health bars.</summary>
        public event Action<float, float> HealthChanged;

        public override int TickOrder => ModuleTickOrder.Health;

        public float Current => current;

        public float Max => max;

        /// <summary>Health as 0..1, for UI.</summary>
        public float Normalized => max > 0f ? current / max : 0f;

        public float Poise => poise;

        public bool IsAlive { get; private set; }

        /// <summary>True while a post-hit invulnerability window is open.</summary>
        public bool IsInInvulnerabilityWindow => invulnerabilityTimer > 0f;

        public EnemyStats Stats => stats;

        // --- IHealth ---
        // Explicit implementations bridge this component's own names onto the shared
        // interface, so a heal effect or a health bar can treat any actor identically
        // without EnemyHealth's existing API changing.

        bool IHealth.IsInvulnerable => IsInInvulnerabilityWindow;

        private event Action healthDied;

        event Action IHealth.Died
        {
            add => healthDied += value;
            remove => healthDied -= value;
        }

        float IHealth.Heal(float amount)
        {
            float before = current;
            Heal(amount);
            return current - before;
        }

        void IHealth.RestoreToFull() => ResetForLife();

        public DamageTeam Team => Owner != null ? Owner.Team : DamageTeam.Enemy;

        public Transform Transform => transform;

        protected override void OnBind()
        {
            stats = statsOverride != null ? statsOverride : Owner.Definition != null ? Owner.Definition.Stats : null;

            if (stats == null)
            {
                Debug.LogWarning($"EnemyHealth on '{name}' has no EnemyStats; falling back to defaults.", this);
                stats = ScriptableObject.CreateInstance<EnemyStats>();
            }

            Configure(stats);
        }

        /// <summary>
        /// Applies a stats asset and refills to full. Called during binding, and directly
        /// by tests and by spawners that scale an encounter.
        /// </summary>
        public void Configure(EnemyStats newStats)
        {
            if (newStats != null)
            {
                stats = newStats;
            }

            max = Mathf.Max(1f, stats.MaxHealth);
            ResetForLife();
        }

        /// <summary>Restores full health and poise. Used on spawn and on pool reuse.</summary>
        public void ResetForLife()
        {
            current = max;
            poise = stats.MaxPoise;
            invulnerabilityTimer = 0f;
            poiseRegenDelayTimer = 0f;
            IsAlive = true;
            debugCurrentHealth = current;
            HealthChanged?.Invoke(current, max);
        }

        public override void OnEnemySpawned() => ResetForLife();

        public override void Tick(float deltaTime)
        {
            if (invulnerabilityTimer > 0f)
            {
                invulnerabilityTimer -= deltaTime;
            }

            if (!IsAlive || stats.MaxPoise <= 0f || poise >= stats.MaxPoise)
            {
                return;
            }

            if (poiseRegenDelayTimer > 0f)
            {
                poiseRegenDelayTimer -= deltaTime;
                return;
            }

            poise = Mathf.Min(stats.MaxPoise, poise + stats.PoiseRegenRate * deltaTime);
        }

        /// <summary>Opens an invulnerability window, e.g. for a dodge or a phase change.</summary>
        public void GrantInvulnerability(float seconds) => invulnerabilityTimer = Mathf.Max(invulnerabilityTimer, seconds);

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            current = Mathf.Min(max, current + amount);
            debugCurrentHealth = current;
            HealthChanged?.Invoke(current, max);
        }

        public DamageResult TakeDamage(in DamageInfo info)
        {
            if (!IsAlive)
            {
                return DamageResult.Ignored;
            }

            // Own-team fire is discarded here rather than in every hitbox.
            if ((info.SourceTeam & Team) != 0)
            {
                return DamageResult.Ignored;
            }

            if (IsInInvulnerabilityWindow && !info.Has(DamageFlags.IgnoreInvulnerability))
            {
                return DamageResult.Ignored;
            }

            DefenseController defense = Owner != null ? Owner.Defense : null;
            DefenseEvaluation evaluation = defense != null ? defense.Evaluate(in info) : DefenseEvaluation.Default;

            var result = new DamageResult
            {
                Blocked = evaluation.Blocked,
                ReflectedDamage = evaluation.ReflectDamage,
            };

            if (evaluation.Immune)
            {
                result.Immune = true;
                result.Reaction = evaluation.Blocked ? HitReaction.Blocked : HitReaction.Immune;
                ApplyReflect(in info, evaluation.ReflectDamage);
                Damaged?.Invoke(info, result);
                return result;
            }

            float applied = evaluation.Apply(info.Amount);
            if (applied > 0f)
            {
                current = Mathf.Max(0f, current - applied);
                debugCurrentHealth = current;
                result.Applied = true;
                result.AmountApplied = applied;
                HealthChanged?.Invoke(current, max);
            }

            bool staggered = EvaluatePoise(in info, in evaluation);
            result.Staggered = staggered;

            if (!info.Has(DamageFlags.NoInvulnerabilityWindow) && applied > 0f)
            {
                GrantInvulnerability(stats.HitInvulnerability);
            }

            ApplyKnockback(in info, in evaluation);
            ApplyReflect(in info, evaluation.ReflectDamage);

            if (current <= 0f)
            {
                result.Killed = true;
                result.Reaction = HitReaction.Death;
                Damaged?.Invoke(info, result);
                Die(in info);
                return result;
            }

            result.Reaction = evaluation.Blocked ? HitReaction.Blocked
                : staggered ? HitReaction.Stagger
                : info.KnockbackForce > 0f && !evaluation.SuppressKnockback ? HitReaction.Knockback
                : stats.FlinchOnHit ? HitReaction.Flinch
                : HitReaction.None;

            Damaged?.Invoke(info, result);

            if (staggered)
            {
                Staggered?.Invoke(info);
            }

            return result;
        }

        private bool EvaluatePoise(in DamageInfo info, in DefenseEvaluation evaluation)
        {
            if (evaluation.SuppressStagger || info.Has(DamageFlags.NoStagger))
            {
                return false;
            }

            if (stats.MaxPoise <= 0f)
            {
                return true;
            }

            poise -= info.PoiseDamage;
            poiseRegenDelayTimer = stats.PoiseRegenDelay;

            if (poise > 0f)
            {
                return false;
            }

            poise = stats.MaxPoise;
            return true;
        }

        private void ApplyKnockback(in DamageInfo info, in DefenseEvaluation evaluation)
        {
            if (evaluation.SuppressKnockback || info.Has(DamageFlags.NoKnockback) || info.KnockbackForce <= 0f)
            {
                return;
            }

            IKnockbackReceiver receiver = Owner != null ? Owner.KnockbackReceiver : null;
            if (receiver == null)
            {
                return;
            }

            float force = info.KnockbackForce * (1f - Mathf.Clamp01(stats.KnockbackResistance));
            if (force <= 0f)
            {
                return;
            }

            Vector2 direction = info.KnockbackDirection;
            if (direction == Vector2.zero)
            {
                direction = ((Vector2)transform.position - info.Origin).normalized;
            }

            receiver.ApplyKnockback(direction, force, 0.2f);
        }

        private void ApplyReflect(in DamageInfo info, float reflectDamage)
        {
            if (reflectDamage <= 0f || info.Attacker == null)
            {
                return;
            }

            var attackerDamageable = info.Attacker.GetComponentInParent<IDamageable>();
            if (attackerDamageable == null || !attackerDamageable.IsAlive)
            {
                return;
            }

            DamageInfo reflected = DamageInfo.Create(reflectDamage, Team, transform.position, gameObject);
            reflected.Type = DamageType.Contact;
            reflected.AimKnockbackFrom(attackerDamageable.Transform.position, 4f, 0.3f);
            attackerDamageable.TakeDamage(in reflected);
        }

        /// <summary>Kills the enemy immediately, bypassing defenses.</summary>
        public void Kill()
        {
            if (!IsAlive)
            {
                return;
            }

            DamageInfo info = DamageInfo.Create(current, DamageTeam.Neutral, transform.position);
            current = 0f;
            Die(in info);
        }

        private void Die(in DamageInfo info)
        {
            IsAlive = false;
            current = 0f;
            debugCurrentHealth = 0f;
            HealthChanged?.Invoke(current, max);
            Died?.Invoke(info);
            healthDied?.Invoke();
        }
    }
}
