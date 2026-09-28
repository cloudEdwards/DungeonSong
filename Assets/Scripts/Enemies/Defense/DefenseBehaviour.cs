using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Shared plumbing for defenses that can be switched on and off, either for a fixed
    /// duration or until something switches them back. States and attacks drive these;
    /// they never drive themselves, which keeps "when do I shield?" a design decision
    /// rather than a hard-coded one.
    /// </summary>
    public abstract class DefenseBehaviour : EnemyModule, IDamageModifier
    {
        [Header("Activation")]
        [SerializeField, Tooltip("Active from spawn, without anything having to switch it on. Use for permanent armour.")]
        private bool activeByDefault;

        private float timer;
        private bool active;

        public abstract int ModifierOrder { get; }

        public bool IsActive => active;

        /// <summary>Seconds remaining on a timed activation, or 0 when untimed.</summary>
        public float Remaining => Mathf.Max(0f, timer);

        public override int TickOrder => ModuleTickOrder.Health;

        public override void OnEnemySpawned()
        {
            active = activeByDefault;
            timer = 0f;
            OnActiveChanged(active);
        }

        /// <summary>Switches the defense on. <paramref name="duration"/> 0 means "until told otherwise".</summary>
        public void Activate(float duration = 0f)
        {
            timer = duration;
            if (!active)
            {
                active = true;
                OnActiveChanged(true);
            }
        }

        public void Deactivate()
        {
            timer = 0f;
            if (active)
            {
                active = false;
                OnActiveChanged(false);
            }
        }

        public override void Tick(float deltaTime)
        {
            if (!active || timer <= 0f)
            {
                return;
            }

            timer -= deltaTime;
            if (timer <= 0f)
            {
                Deactivate();
            }
        }

        /// <summary>Hook for animation, VFX or collider changes when the defense toggles.</summary>
        protected virtual void OnActiveChanged(bool isActive) { }

        public abstract void ModifyIncoming(in DamageInfo info, ref DefenseEvaluation evaluation);
    }
}
