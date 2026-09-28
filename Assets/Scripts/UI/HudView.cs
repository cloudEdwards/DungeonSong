using UnityEngine;
using DungeonSong.Player;

namespace DungeonSong.UI
{
    /// <summary>
    /// Base class for one piece of the HUD.
    /// <para>
    /// Views are found and bound by <see cref="PlayerHud"/>, so adding a new readout —
    /// a soul meter, a boss health bar, an equipped-tool strip — is a new component on a
    /// child object. Nothing about the HUD root changes, which is the same composition rule
    /// the player and enemy frameworks follow.
    /// </para>
    /// </summary>
    public abstract class HudView : MonoBehaviour
    {
        /// <summary>The player this view is reading. Valid from <see cref="OnBind"/> onward.</summary>
        protected PlayerActor Player { get; private set; }

        /// <summary>True once a player has been bound.</summary>
        public bool IsBound => Player != null;

        internal void Bind(PlayerActor player)
        {
            if (Player != null)
            {
                OnUnbind();
            }

            Player = player;

            if (Player != null)
            {
                OnBind();
            }
        }

        /// <summary>Subscribe to events and do first-time layout here.</summary>
        protected virtual void OnBind() { }

        /// <summary>Unsubscribe here. Called before rebinding and on destruction.</summary>
        protected virtual void OnUnbind() { }

        /// <summary>
        /// Per-frame refresh, driven by <see cref="PlayerHud"/>. Prefer events for values
        /// that change rarely; use this for continuously changing things like cooldowns.
        /// </summary>
        public virtual void Tick(float deltaTime) { }

        protected virtual void OnDestroy()
        {
            if (Player != null)
            {
                OnUnbind();
                Player = null;
            }
        }
    }
}
