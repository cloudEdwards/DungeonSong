using System;
using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Opens a <see cref="Hitbox"/> during a slice of an Animator state, identified by state
    /// name and normalized time.
    /// <para>
    /// This exists so an actor whose attacks are already animation-triggered can deal damage
    /// without its controller script or its imported clips being touched: no animation
    /// events to author, no state machine behaviours, no edits to the driving code. It is
    /// the intended bridge for the player character until the player's own combat pass
    /// replaces it with authored clip events, which the attack framework also supports.
    /// </para>
    /// </summary>
    public class AnimatorHitboxDriver : MonoBehaviour
    {
        /// <summary>One damaging window inside one Animator state.</summary>
        [Serializable]
        public struct Window
        {
            [Tooltip("Animator state name exactly as it appears in the controller, e.g. 'Attack1'.")]
            public string StateName;

            [Range(0f, 1f), Tooltip("Normalized time the hitbox opens.")]
            public float From;

            [Range(0f, 1f), Tooltip("Normalized time the hitbox closes.")]
            public float To;

            [Min(0f), Tooltip("Damage for this window. Lets a three-hit combo escalate.")]
            public float Damage;

            [Min(0f)] public float PoiseDamage;

            [Min(0f)] public float KnockbackForce;

            [Range(0f, 1f)] public float KnockbackUpwardBias;
        }

        [Header("Wiring")]
        [SerializeField, Tooltip("Animator to watch. Leave empty to search this object and its parents.")]
        private Animator animator;

        [SerializeField, Tooltip("Hitbox to open and close. Leave empty to search children.")]
        private Hitbox hitbox;

        [SerializeField, Min(0), Tooltip("Animator layer to watch.")]
        private int layerIndex;

        [Header("Attack Identity")]
        [SerializeField, Tooltip("Team of the attacker.")]
        private DamageTeam sourceTeam = DamageTeam.Player;

        [SerializeField, Tooltip("Teams this can damage.")]
        private DamageTeam targetTeams = DamageTeam.Enemy;

        [SerializeField] private DamageType damageType = DamageType.Physical;

        [Header("Windows")]
        [SerializeField, Tooltip("One entry per attack state. Times are fractions of the clip.")]
        private Window[] windows = Array.Empty<Window>();

        private int[] stateHashes;
        private int activeWindow = -1;
        private int lastStateHash;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInParent<Animator>();
            }

            if (hitbox == null)
            {
                hitbox = GetComponentInChildren<Hitbox>();
            }

            // Hashed once; comparing hashes per frame costs nothing.
            stateHashes = new int[windows.Length];
            for (int i = 0; i < windows.Length; i++)
            {
                stateHashes[i] = Animator.StringToHash(windows[i].StateName);
            }
        }

        private void Update()
        {
            if (animator == null || hitbox == null || windows.Length == 0)
            {
                return;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
            int hash = info.shortNameHash;

            // Re-entering the same state (combo repeat) must re-arm the window.
            if (hash != lastStateHash)
            {
                lastStateHash = hash;
                CloseWindow();
            }

            int matched = -1;
            float normalized = info.normalizedTime % 1f;

            for (int i = 0; i < stateHashes.Length; i++)
            {
                if (stateHashes[i] != hash)
                {
                    continue;
                }

                if (normalized >= windows[i].From && normalized <= windows[i].To)
                {
                    matched = i;
                }

                break;
            }

            if (matched == activeWindow)
            {
                return;
            }

            if (matched < 0)
            {
                CloseWindow();
                return;
            }

            OpenWindow(matched);
        }

        private void OpenWindow(int index)
        {
            Window window = windows[index];
            activeWindow = index;

            hitbox.TargetTeams = targetTeams;

            DamageInfo info = DamageInfo.Create(window.Damage, sourceTeam, transform.position, gameObject);
            info.Type = damageType;
            info.PoiseDamage = window.PoiseDamage > 0f ? window.PoiseDamage : window.Damage;
            info.KnockbackForce = window.KnockbackForce;
            hitbox.Activate(in info);
        }

        private void CloseWindow()
        {
            if (activeWindow < 0)
            {
                return;
            }

            activeWindow = -1;
            hitbox.Deactivate();
        }

        private void OnDisable() => CloseWindow();
    }
}
