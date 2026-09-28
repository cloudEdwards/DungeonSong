using System.Collections.Generic;
using UnityEngine;
using DungeonSong.Player;

namespace DungeonSong.UI
{
    /// <summary>
    /// The player's ability bar, in the spirit of Silksong's tool strip: one entry per
    /// equipped ability, showing what it is, which key uses it, and whether it is available.
    /// <para>
    /// Entries are cloned from a template at bind time, so the bar reflects whatever the
    /// player currently has equipped. Acquiring the thirtieth spell adds a slot to the
    /// loadout and the bar follows; no UI work is needed per ability.
    /// </para>
    /// </summary>
    public class AbilityBarView : HudView
    {
        [Header("Template")]
        [SerializeField, Tooltip("Slot entry to clone, as a child of this object. It is hidden at runtime and used as the prototype.")]
        private AbilitySlotView slotTemplate;

        [Header("Input Labels")]
        [SerializeField, Tooltip("Reads key labels from the player's input source so the bar always shows the real bindings.")]
        private bool readKeysFromInput = true;

        [SerializeField, Tooltip("Fallback labels, used when the key cannot be read.")]
        private string[] fallbackKeyLabels = { "F", "Q", "1", "2" };

        [Header("Refresh")]
        [SerializeField, Min(0f), Tooltip("Seconds between refreshes. Cooldown sweeps look smooth well below every frame.")]
        private float refreshInterval = 0.05f;

        private readonly List<AbilitySlotView> slots = new List<AbilitySlotView>(4);
        private float refreshTimer;

        protected override void OnBind()
        {
            BuildSlots();
        }

        private void BuildSlots()
        {
            if (slotTemplate == null)
            {
                Debug.LogWarning($"AbilityBarView on '{name}' has no slot template; the bar will stay empty.", this);
                return;
            }

            slotTemplate.gameObject.SetActive(false);

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    Destroy(slots[i].gameObject);
                }
            }

            slots.Clear();

            AbilityLoadout loadout = Player.Abilities;
            if (loadout == null)
            {
                return;
            }

            for (int i = 0; i < loadout.Slots.Count; i++)
            {
                AbilityBehaviour ability = loadout.Slots[i];
                if (ability == null || ability.Definition == null)
                {
                    continue;
                }

                AbilitySlotView entry = Instantiate(slotTemplate, slotTemplate.transform.parent);
                entry.name = $"AbilitySlot_{i}";
                entry.Bind(ability, ResolveKeyLabel(i));
                entry.Refresh();
                slots.Add(entry);
            }
        }

        private string ResolveKeyLabel(int slot)
        {
            if (readKeysFromInput)
            {
                var source = Player.GetComponent<LegacyInputSource>();
                if (source != null)
                {
                    string label = source.GetAbilityKeyLabel(slot);
                    if (!string.IsNullOrEmpty(label))
                    {
                        return label;
                    }
                }
            }

            return slot >= 0 && slot < fallbackKeyLabels.Length ? fallbackKeyLabels[slot] : string.Empty;
        }

        public override void Tick(float deltaTime)
        {
            refreshTimer -= deltaTime;
            if (refreshTimer > 0f)
            {
                return;
            }

            refreshTimer = refreshInterval;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].Refresh();
                }
            }
        }

        /// <summary>Rebuilds the bar. Call after the player acquires or swaps an ability.</summary>
        public void Rebuild()
        {
            if (IsBound)
            {
                BuildSlots();
            }
        }
    }
}
