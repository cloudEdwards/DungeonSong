using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>Maps one semantic animation key onto one Animator parameter.</summary>
    [Serializable]
    public struct PlayerAnimationBinding
    {
        [Tooltip("Semantic key used by attacks and abilities, e.g. 'attack_forward'.")]
        public string Key;

        [Tooltip("Animator parameter name on the player's controller.")]
        public string Parameter;
    }

    /// <summary>
    /// Translates semantic animation calls into Animator parameters on the player's rig.
    /// Parameter names are hashed once at bind time; unmapped keys are ignored rather than
    /// throwing, so a half-rigged ability still runs and still deals damage.
    /// </summary>
    public class PlayerAnimatorAdapter : PlayerModule, IPlayerAnimator
    {
        [Header("Animator")]
        [SerializeField, Tooltip("Animator to drive. Leave empty to search this object and its children.")]
        private Animator animator;

        [Header("Bindings")]
        [SerializeField, Tooltip("Semantic key to Animator trigger. Attacks and abilities reference the key, never the parameter.")]
        private PlayerAnimationBinding[] actionBindings = Array.Empty<PlayerAnimationBinding>();

        [SerializeField, Tooltip("Semantic key to Animator bool, for sustained states.")]
        private PlayerAnimationBinding[] flagBindings = Array.Empty<PlayerAnimationBinding>();

        [Header("Diagnostics")]
        [SerializeField, Tooltip("Log once per unmapped key. Helpful while rigging, noisy afterwards.")]
        private bool warnOnMissingKeys;

        private readonly Dictionary<string, int> actionHashes = new Dictionary<string, int>();
        private readonly Dictionary<string, int> flagHashes = new Dictionary<string, int>();
        private readonly HashSet<string> warned = new HashSet<string>();
        private bool hasAnimator;

        public override int TickOrder => PlayerTickOrder.Animation;

        protected override void OnBind()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            hasAnimator = animator != null;
            Cache(actionBindings, actionHashes);
            Cache(flagBindings, flagHashes);
        }

        private static void Cache(PlayerAnimationBinding[] bindings, Dictionary<string, int> into)
        {
            into.Clear();
            if (bindings == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                PlayerAnimationBinding b = bindings[i];
                if (!string.IsNullOrEmpty(b.Key) && !string.IsNullOrEmpty(b.Parameter))
                {
                    into[b.Key] = Animator.StringToHash(b.Parameter);
                }
            }
        }

        public void PlayAction(string actionKey)
        {
            if (!hasAnimator || string.IsNullOrEmpty(actionKey))
            {
                return;
            }

            if (actionHashes.TryGetValue(actionKey, out int hash))
            {
                animator.SetTrigger(hash);
                return;
            }

            Warn(actionKey);
        }

        public void SetFlag(string flagKey, bool value)
        {
            if (!hasAnimator || string.IsNullOrEmpty(flagKey))
            {
                return;
            }

            if (flagHashes.TryGetValue(flagKey, out int hash))
            {
                animator.SetBool(hash, value);
                return;
            }

            Warn(flagKey);
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            if (hasAnimator)
            {
                animator.speed = multiplier;
            }
        }

        public bool IsPlaying(string actionKey)
        {
            if (!hasAnimator || !actionHashes.TryGetValue(actionKey, out int hash))
            {
                return false;
            }

            return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;
        }

        private void Warn(string key)
        {
            if (warnOnMissingKeys && warned.Add(key))
            {
                Debug.LogWarning($"PlayerAnimatorAdapter on '{name}' has no binding for key '{key}'.", this);
            }
        }
    }
}
