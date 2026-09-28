using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Maps one semantic animation key onto one Animator parameter.
    /// </summary>
    [Serializable]
    public struct AnimationBinding
    {
        [Tooltip("Semantic key used in code and in attack definitions, e.g. 'attack_slash'.")]
        public string Key;

        [Tooltip("Animator parameter name on this enemy's controller.")]
        public string Parameter;
    }

    /// <summary>
    /// Translates semantic animation calls into Animator parameters. Parameter names are
    /// hashed once at bind time, and unmapped keys are ignored rather than throwing, so a
    /// half-rigged enemy still runs.
    /// <para>
    /// Bindings live on the component rather than in a ScriptableObject because they are
    /// specific to one prefab's Animator controller; there is nothing to share.
    /// </para>
    /// </summary>
    public class AnimatorAdapter : EnemyModule, IAnimatorAdapter
    {
        [Header("Animator")]
        [SerializeField, Tooltip("Animator to drive. Leave empty to search this object and its children.")]
        private Animator animator;

        [Header("Locomotion Parameters")]
        [SerializeField, Tooltip("Float parameter fed normalized movement speed. Leave empty to skip.")]
        private string locomotionSpeedParameter = "Speed";

        [SerializeField, Tooltip("Float parameter fed vertical speed. Leave empty to skip.")]
        private string verticalSpeedParameter = "";

        [SerializeField, Tooltip("Bool parameter fed grounded state. Leave empty to skip.")]
        private string groundedParameter = "";

        [Header("Action Bindings")]
        [SerializeField, Tooltip("Semantic key to Animator trigger name. Keys are referenced by attacks, defenses and states.")]
        private AnimationBinding[] actionBindings = Array.Empty<AnimationBinding>();

        [SerializeField, Tooltip("Semantic key to Animator bool name, for sustained states.")]
        private AnimationBinding[] flagBindings = Array.Empty<AnimationBinding>();

        [Header("Diagnostics")]
        [SerializeField, Tooltip("Log once per unmapped key. Helpful while rigging, noisy afterwards.")]
        private bool warnOnMissingKeys;

        private readonly Dictionary<string, int> actionHashes = new Dictionary<string, int>();
        private readonly Dictionary<string, int> flagHashes = new Dictionary<string, int>();
        private readonly HashSet<string> warned = new HashSet<string>();

        private int locomotionHash;
        private int verticalHash;
        private int groundedHash;
        private bool hasAnimator;

        public override int TickOrder => ModuleTickOrder.Animation;

        protected override void OnBind()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            hasAnimator = animator != null;

            locomotionHash = Hash(locomotionSpeedParameter);
            verticalHash = Hash(verticalSpeedParameter);
            groundedHash = Hash(groundedParameter);

            CacheBindings(actionBindings, actionHashes);
            CacheBindings(flagBindings, flagHashes);
        }

        private static void CacheBindings(AnimationBinding[] bindings, Dictionary<string, int> into)
        {
            into.Clear();
            if (bindings == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                AnimationBinding binding = bindings[i];
                if (!string.IsNullOrEmpty(binding.Key) && !string.IsNullOrEmpty(binding.Parameter))
                {
                    into[binding.Key] = Animator.StringToHash(binding.Parameter);
                }
            }
        }

        private static int Hash(string parameter) => string.IsNullOrEmpty(parameter) ? 0 : Animator.StringToHash(parameter);

        public void SetLocomotionSpeed(float normalizedSpeed)
        {
            if (hasAnimator && locomotionHash != 0)
            {
                animator.SetFloat(locomotionHash, normalizedSpeed);
            }
        }

        public void SetVerticalSpeed(float verticalSpeed)
        {
            if (hasAnimator && verticalHash != 0)
            {
                animator.SetFloat(verticalHash, verticalSpeed);
            }
        }

        public void SetGrounded(bool grounded)
        {
            if (hasAnimator && groundedHash != 0)
            {
                animator.SetBool(groundedHash, grounded);
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

        private void Warn(string key)
        {
            if (warnOnMissingKeys && warned.Add(key))
            {
                Debug.LogWarning($"AnimatorAdapter on '{name}' has no binding for key '{key}'.", this);
            }
        }
    }
}
