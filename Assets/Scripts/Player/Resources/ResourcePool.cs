using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Runtime pools for every resource the player carries.
    /// <para>
    /// Values live here, per instance, never in the <see cref="ResourceDefinition"/> asset:
    /// definitions are shared configuration, and writing runtime state into them would make
    /// every future save and every second player share one Loyalty bar.
    /// </para>
    /// </summary>
    public class ResourcePool : PlayerModule
    {
        [Header("Pools")]
        [SerializeField, Tooltip("Resources this player has. Abilities reference definitions, not this list.")]
        private ResourceDefinition[] resources = Array.Empty<ResourceDefinition>();

        /// <summary>Raised on any change, as (definition, current, max).</summary>
        public event Action<ResourceDefinition, float, float> ResourceChanged;

        // Values by resource id for the whole play session. The player object is rebuilt
        // in every scene, so without this a door would refill spell slots and wipe Loyalty.
        // Health survives scene loads through PlayerDataDto for the same reason.
        private static readonly Dictionary<string, float> session = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearSession() => session.Clear();

        private readonly Dictionary<ResourceDefinition, float> current = new Dictionary<ResourceDefinition, float>();
        private readonly Dictionary<ResourceDefinition, float> regenDelay = new Dictionary<ResourceDefinition, float>();
        private readonly List<ResourceDefinition> tickBuffer = new List<ResourceDefinition>(4);

        public override int TickOrder => PlayerTickOrder.Resources;

        /// <summary>Every resource this player carries.</summary>
        public IReadOnlyList<ResourceDefinition> Resources => resources;

        protected override void OnBind() => LoadSession();

        public override void OnPlayerInitialized() => Owner.Respawned += OnRespawned;

        private void OnDestroy()
        {
            if (Owner != null)
            {
                Owner.Respawned -= OnRespawned;
            }
        }

        public override void OnPlayerDied()
        {
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i] != null && resources[i].EmptiedOnDeath && current.ContainsKey(resources[i]))
                {
                    SetAmount(resources[i], 0f);
                }
            }
        }

        // Waking at a checkpoint after death restores what a long rest would, without the save.
        private void OnRespawned(PlayerActor player) => RestoreFor(RestType.Long);

        /// <summary>Current amount, or 0 for a resource this player does not carry.</summary>
        public float GetAmount(ResourceDefinition definition)
        {
            return definition != null && current.TryGetValue(definition, out float value) ? value : 0f;
        }

        public float GetMax(ResourceDefinition definition) => definition != null ? definition.MaxAmount : 0f;

        /// <summary>Current amount as 0..1, for UI bars.</summary>
        public float GetNormalized(ResourceDefinition definition)
        {
            float max = GetMax(definition);
            return max > 0f ? GetAmount(definition) / max : 0f;
        }

        public bool CanAfford(in ResourceCost cost)
        {
            return cost.IsFree || GetAmount(cost.Resource) >= cost.Amount;
        }

        /// <summary>Spends a cost if it can be paid. Returns false and spends nothing otherwise.</summary>
        public bool TrySpend(in ResourceCost cost)
        {
            if (cost.IsFree)
            {
                return true;
            }

            if (!current.TryGetValue(cost.Resource, out float value) || value < cost.Amount)
            {
                return false;
            }

            SetAmount(cost.Resource, value - cost.Amount);
            regenDelay[cost.Resource] = cost.Resource.RegenDelay;
            return true;
        }

        /// <summary>Adds to a pool, clamped to its maximum.</summary>
        public void Restore(ResourceDefinition definition, float amount)
        {
            if (definition == null || amount <= 0f || !current.ContainsKey(definition))
            {
                return;
            }

            SetAmount(definition, Mathf.Min(definition.MaxAmount, current[definition] + amount));
        }

        /// <summary>True when the pool is at its maximum.</summary>
        public bool IsFull(ResourceDefinition definition)
        {
            return definition != null && current.TryGetValue(definition, out float value) && value >= definition.MaxAmount;
        }

        /// <summary>
        /// Refills every pool whose <see cref="ResourceDefinition.RefilledBy"/> includes
        /// <paramref name="rest"/>. Pools that do not recover on this kind of rest are untouched.
        /// </summary>
        public void RestoreFor(RestType rest)
        {
            for (int i = 0; i < resources.Length; i++)
            {
                ResourceDefinition definition = resources[i];
                if (definition != null && (definition.RefilledBy & rest) != 0 && current.ContainsKey(definition))
                {
                    SetAmount(definition, definition.MaxAmount);
                }
            }
        }

        /// <summary>Resets every pool to its starting amount. For a new game, not for resting.</summary>
        public void RestoreAll()
        {
            regenDelay.Clear();

            for (int i = 0; i < resources.Length; i++)
            {
                ResourceDefinition definition = resources[i];
                if (definition != null)
                {
                    SetAmount(definition, Mathf.Clamp(definition.StartingAmount, 0f, definition.MaxAmount));
                }
            }
        }

        private void LoadSession()
        {
            current.Clear();
            regenDelay.Clear();

            for (int i = 0; i < resources.Length; i++)
            {
                ResourceDefinition definition = resources[i];
                if (definition == null)
                {
                    continue;
                }

                float value = session.TryGetValue(definition.Id, out float saved) ? saved : definition.StartingAmount;
                SetAmount(definition, Mathf.Clamp(value, 0f, definition.MaxAmount));
            }
        }

        public override void Tick(float deltaTime)
        {
            if (current.Count == 0)
            {
                return;
            }

            tickBuffer.Clear();
            foreach (KeyValuePair<ResourceDefinition, float> pair in current)
            {
                if (pair.Key.RegenPerSecond > 0f && pair.Value < pair.Key.MaxAmount)
                {
                    tickBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < tickBuffer.Count; i++)
            {
                ResourceDefinition definition = tickBuffer[i];

                if (regenDelay.TryGetValue(definition, out float delay) && delay > 0f)
                {
                    regenDelay[definition] = delay - deltaTime;
                    continue;
                }

                SetAmount(definition, Mathf.Min(definition.MaxAmount, current[definition] + definition.RegenPerSecond * deltaTime));
            }
        }

        private void SetAmount(ResourceDefinition definition, float value)
        {
            current[definition] = value;
            session[definition.Id] = value;
            ResourceChanged?.Invoke(definition, value, definition.MaxAmount);
        }
    }
}
