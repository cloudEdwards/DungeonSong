using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Standard tick order for modules. Perception runs before the brain so decisions
    /// see fresh information; movement runs last in the physics step so it consumes the
    /// intent the brain just produced.
    /// </summary>
    public static class ModuleTickOrder
    {
        public const int Activation = -100;
        public const int Health = 0;
        public const int Perception = 100;
        public const int Brain = 200;
        public const int Attacks = 300;
        public const int Movement = 400;
        public const int Animation = 900;
    }

    /// <summary>
    /// Base class for every capability bolted onto an <see cref="Enemy"/>.
    /// <para>
    /// Modules do not implement Unity's Update or FixedUpdate. The owning
    /// <see cref="Enemy"/> ticks them in <see cref="TickOrder"/>, which gives
    /// deterministic ordering and collapses what would be eight or more engine callbacks
    /// per enemy into two. With a hundred enemies on screen that difference is the
    /// difference between a frame budget and a slideshow.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public abstract class EnemyModule : MonoBehaviour
    {
        /// <summary>The enemy this module belongs to. Valid from <see cref="OnBind"/> onward.</summary>
        public Enemy Owner { get; private set; }

        /// <summary>Lower values tick earlier. See <see cref="ModuleTickOrder"/>.</summary>
        public virtual int TickOrder => 0;

        /// <summary>
        /// When true this module keeps ticking while the enemy is dormant. Only
        /// activation logic should need it.
        /// </summary>
        public virtual bool TickWhileDormant => false;

        internal void Bind(Enemy owner)
        {
            Owner = owner;
            OnBind();
        }

        /// <summary>Cache references here. Other modules may not be bound yet.</summary>
        protected virtual void OnBind() { }

        /// <summary>
        /// Called once after every module on the enemy is bound, so cross-module lookups
        /// are safe here.
        /// </summary>
        public virtual void OnEnemyInitialized() { }

        /// <summary>Called each time the enemy enters play, including reuse from a pool.</summary>
        public virtual void OnEnemySpawned() { }

        /// <summary>Called when the enemy leaves play. Reset per-life state here.</summary>
        public virtual void OnEnemyDespawned() { }

        /// <summary>Called once when the enemy dies, before any despawn delay.</summary>
        public virtual void OnEnemyDied() { }

        /// <summary>Per-frame work. Visual and decision logic belongs here.</summary>
        public virtual void Tick(float deltaTime) { }

        /// <summary>Per-physics-step work. Anything touching Rigidbody2D belongs here.</summary>
        public virtual void FixedTick(float fixedDeltaTime) { }
    }
}
