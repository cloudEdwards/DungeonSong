using UnityEngine;
using UnityEngine.UI;
using DungeonSong.Player;

namespace DungeonSong.UI
{
    /// <summary>
    /// One entry in the ability bar: icon, key, cooldown sweep, charges, and whether it can
    /// be used right now.
    /// <para>
    /// Cloned from a template by <see cref="AbilityBarView"/>, so the bar grows with the
    /// player's loadout instead of being laid out by hand. Styling one template restyles
    /// every ability the player will ever acquire.
    /// </para>
    /// </summary>
    public class AbilitySlotView : MonoBehaviour
    {
        [Header("Widgets")]
        [SerializeField, Tooltip("Ability icon, taken from the AbilityDefinition.")]
        private Image iconImage;

        [SerializeField, Tooltip("Radial cooldown overlay. Set its Image Type to Filled, Radial 360.")]
        private Image cooldownOverlay;

        [SerializeField, Tooltip("Key the player presses for this slot.")]
        private Text keyText;

        [SerializeField, Tooltip("Ability name.")]
        private Text nameText;

        [SerializeField, Tooltip("Remaining charges, or a running effect's stacks. Hidden when there is neither.")]
        private Text chargesText;

        [Header("Availability")]
        [SerializeField, Tooltip("Tint when the ability can be used.")]
        private Color readyColor = Color.white;

        [SerializeField, Tooltip("Tint when it cannot: on cooldown, unaffordable, or requirements unmet.")]
        private Color unavailableColor = new Color(0.4f, 0.4f, 0.45f, 1f);

        private AbilityBehaviour ability;

        /// <summary>The ability this entry displays.</summary>
        public AbilityBehaviour Ability => ability;

        /// <summary>Points this entry at an ability and does the one-time setup.</summary>
        public void Bind(AbilityBehaviour target, string keyLabel)
        {
            ability = target;

            bool hasAbility = ability != null && ability.Definition != null;
            gameObject.SetActive(hasAbility);

            if (!hasAbility)
            {
                return;
            }

            AbilityDefinition definition = ability.Definition;

            if (iconImage != null)
            {
                iconImage.sprite = definition.Icon;
                // An ability with no icon art yet still needs a visible slot.
                iconImage.enabled = definition.Icon != null;
            }

            if (nameText != null)
            {
                nameText.text = definition.DisplayName;
            }

            if (keyText != null)
            {
                keyText.text = keyLabel;
            }
        }

        /// <summary>Shows a different key, e.g. after the player picks up a gamepad.</summary>
        public void SetKeyLabel(string keyLabel)
        {
            if (keyText != null)
            {
                keyText.text = keyLabel;
            }
        }

        /// <summary>Refreshes the volatile parts: cooldown, charges, availability.</summary>
        public void Refresh()
        {
            if (ability == null || ability.Definition == null)
            {
                return;
            }

            AbilityDefinition definition = ability.Definition;

            if (cooldownOverlay != null)
            {
                float remaining = ability.CooldownRemaining;
                cooldownOverlay.enabled = remaining > 0f;
                cooldownOverlay.fillAmount = definition.Cooldown > 0f
                    ? Mathf.Clamp01(remaining / definition.Cooldown)
                    : 0f;
            }

            if (chargesText != null)
            {
                // A running effect's counter (Smite's empowered strikes) outranks charges.
                int count = ability.ActiveStacks > 0 ? ability.ActiveStacks : ability.ChargesRemaining;
                bool show = count >= 0;
                chargesText.enabled = show;
                if (show)
                {
                    chargesText.text = count.ToString();
                }
            }

            // CanActivate folds in cooldown, cost and requirements, so "greyed out" means
            // exactly "pressing this now would do nothing".
            bool usable = ability.CanActivate();
            Color tint = usable ? readyColor : unavailableColor;

            if (iconImage != null)
            {
                iconImage.color = tint;
            }

            if (nameText != null)
            {
                nameText.color = tint;
            }
        }
    }
}
