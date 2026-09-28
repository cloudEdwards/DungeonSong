using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>Tick order for player modules. Input first, presentation last.</summary>
    public static class PlayerTickOrder
    {
        public const int Input = 0;
        public const int Resources = 100;
        public const int Combat = 200;
        public const int Abilities = 300;
        public const int Interaction = 400;
        public const int Animation = 900;
    }

    /// <summary>
    /// Base class for every capability bolted onto the player.
    /// <para>
    /// Modules do not implement Unity's Update. <see cref="PlayerActor"/> ticks them in
    /// <see cref="TickOrder"/>, which gives deterministic ordering: input is always read
    /// before combat consumes it, and animation always sees the finished frame.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerActor))]
    public abstract class PlayerModule : MonoBehaviour
    {
        /// <summary>The player this module belongs to. Valid from <see cref="OnBind"/> onward.</summary>
        public PlayerActor Owner { get; private set; }

        /// <summary>Lower values tick earlier. See <see cref="PlayerTickOrder"/>.</summary>
        public virtual int TickOrder => 0;

        internal void Bind(PlayerActor owner)
        {
            Owner = owner;
            OnBind();
        }

        /// <summary>Cache references here. Other modules may not be bound yet.</summary>
        protected virtual void OnBind() { }

        /// <summary>Called once after every module is bound, so cross-module lookups are safe.</summary>
        public virtual void OnPlayerInitialized() { }

        /// <summary>Called when the player spawns or respawns. Reset per-life state here.</summary>
        public virtual void OnPlayerSpawned() { }

        /// <summary>Called when the player dies, before any respawn.</summary>
        public virtual void OnPlayerDied() { }

        public virtual void Tick(float deltaTime) { }

        public virtual void FixedTick(float fixedDeltaTime) { }
    }
}
