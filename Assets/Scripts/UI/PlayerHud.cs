using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Player;

namespace DungeonSong.UI
{
    /// <summary>
    /// Finds the player and binds every <see cref="HudView"/> beneath this canvas to it.
    /// <para>
    /// The HUD is deliberately passive: it never tells the game anything, it only reads.
    /// That keeps UI out of the gameplay dependency graph entirely — the player framework
    /// has no idea a HUD exists, so the HUD can be replaced, restyled, or switched off
    /// without touching a single gameplay system.
    /// </para>
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField, Tooltip("Player to display. Leave empty to find the one in the scene.")]
        private PlayerActor player;

        [SerializeField, Tooltip("Keep looking for a player until one appears. Needed when the HUD loads before the player.")]
        private bool waitForPlayer = true;

        [SerializeField, Min(0.05f), Tooltip("Seconds between attempts to find the player.")]
        private float rebindInterval = 0.25f;

        private readonly List<HudView> views = new List<HudView>(8);
        private float rebindTimer;

        /// <summary>The player currently displayed, or null.</summary>
        public PlayerActor Player => player;

        private void Awake() => GetComponentsInChildren(true, views);

        private void Start() => TryBind();

        private void Update()
        {
            if (player == null)
            {
                if (!waitForPlayer)
                {
                    return;
                }

                rebindTimer -= Time.unscaledDeltaTime;
                if (rebindTimer <= 0f)
                {
                    rebindTimer = rebindInterval;
                    TryBind();
                }

                return;
            }

            // Unscaled, so cooldown sweeps and prompts keep animating through hit-stop.
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] != null && views[i].isActiveAndEnabled)
                {
                    views[i].Tick(dt);
                }
            }
        }

        private void TryBind()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerActor>();
            }

            if (player == null)
            {
                return;
            }

            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] != null)
                {
                    views[i].Bind(player);
                }
            }
        }

        /// <summary>Points the HUD at a different player, e.g. after a respawn that replaces the object.</summary>
        public void Rebind(PlayerActor newPlayer)
        {
            player = newPlayer;
            TryBind();
        }
    }
}
