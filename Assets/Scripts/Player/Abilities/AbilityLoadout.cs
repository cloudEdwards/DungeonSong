using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// What the player currently has equipped, and the bridge from an input slot to an
    /// ability.
    /// <para>
    /// Progression later becomes a matter of adding and removing entries here, rather than
    /// of the player controller learning about new spells.
    /// </para>
    /// </summary>
    public class AbilityLoadout : PlayerModule
    {
        [Header("Slots")]
        [SerializeField, Tooltip("Abilities in slot order. Slot 0 is the first ability key.")]
        private AbilityBehaviour[] slots = Array.Empty<AbilityBehaviour>();

        /// <summary>Raised when an ability in a slot is activated, as (slot, ability).</summary>
        public event Action<int, AbilityBehaviour> AbilityActivated;

        private PlayerInputRouter input;

        public override int TickOrder => PlayerTickOrder.Abilities;

        /// <summary>Equipped abilities, in slot order. Entries may be null.</summary>
        public IReadOnlyList<AbilityBehaviour> Slots => slots;

        /// <summary>True while any equipped ability is mid-cast.</summary>
        public bool IsCasting
        {
            get
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].IsRunning)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        protected override void OnBind() => input = GetComponent<PlayerInputRouter>();

        public override void Tick(float deltaTime)
        {
            if (input == null || !input.TryConsumeAbility(out AbilityIntent intent))
            {
                return;
            }

            TryActivateSlot(intent.Slot);
        }

        /// <summary>
        /// Activates the ability in a slot. Public so UI buttons, items and scripted
        /// sequences can trigger abilities without faking input.
        /// </summary>
        public bool TryActivateSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length)
            {
                return false;
            }

            AbilityBehaviour ability = slots[slot];
            if (ability == null || !ability.TryActivate())
            {
                return false;
            }

            AbilityActivated?.Invoke(slot, ability);
            return true;
        }

        /// <summary>Finds an equipped ability by its definition id.</summary>
        public AbilityBehaviour Find(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return null;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].Definition != null && slots[i].Definition.Id == abilityId)
                {
                    return slots[i];
                }
            }

            return null;
        }

        /// <summary>Puts an ability into a slot at runtime, for pickups and progression.</summary>
        public void Equip(int slot, AbilityBehaviour ability)
        {
            if (slot < 0 || slot >= slots.Length)
            {
                return;
            }

            slots[slot] = ability;
        }

        /// <summary>Cancels any cast in progress. Used on death, damage interrupts and rest.</summary>
        public void CancelAll()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i]?.Cancel();
            }
        }

        /// <summary>Refills every equipped ability's charges. Called by rest points.</summary>
        public void RestoreAllCharges()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i]?.ResetCharges();
            }
        }

        public override void OnPlayerDied() => CancelAll();
    }
}
