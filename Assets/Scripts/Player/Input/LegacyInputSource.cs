using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Reads input through Unity's legacy Input API, matching what PlayerController already
    /// uses so both agree about the same frame.
    /// <para>
    /// This is the only file in the player framework that touches a concrete input API.
    /// Moving to the Input System package means adding a sibling implementation of
    /// <see cref="IPlayerInputSource"/> and swapping the component.
    /// </para>
    /// </summary>
    public class LegacyInputSource : MonoBehaviour, IPlayerInputSource
    {
        [Header("Bindings")]
        [SerializeField, Tooltip("Mouse button for the basic attack. 0 = left.")]
        private int attackMouseButton;

        [SerializeField, Tooltip("Keys that activate ability slots, in slot order.")]
        private KeyCode[] abilityKeys = { KeyCode.R, KeyCode.Q, KeyCode.Alpha1, KeyCode.Alpha2 };

        [SerializeField, Tooltip("Key that interacts with campfires and other interactables.")]
        private KeyCode interactKey = KeyCode.E;

        public Vector2 MoveAxis => new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));

        public bool AttackPressed => UnityEngine.Input.GetMouseButtonDown(attackMouseButton);

        public bool AttackHeld => UnityEngine.Input.GetMouseButton(attackMouseButton);

        public bool AttackReleased => UnityEngine.Input.GetMouseButtonUp(attackMouseButton);

        public bool InteractPressed => UnityEngine.Input.GetKeyDown(interactKey);

        public bool AbilityPressed(int slot)
        {
            if (abilityKeys == null || slot < 0 || slot >= abilityKeys.Length)
            {
                return false;
            }

            return UnityEngine.Input.GetKeyDown(abilityKeys[slot]);
        }

        /// <summary>Number of ability slots this binding exposes.</summary>
        public int AbilitySlotCount => abilityKeys != null ? abilityKeys.Length : 0;

        /// <summary>
        /// Display label for the interact key, so prompts stay correct when the binding
        /// changes rather than hard-coding "F" into a string somewhere.
        /// </summary>
        public string InteractKeyLabel => ToLabel(interactKey);

        /// <summary>Display label for an ability slot's key, or empty when the slot is unbound.</summary>
        public string GetAbilityKeyLabel(int slot)
        {
            if (abilityKeys == null || slot < 0 || slot >= abilityKeys.Length)
            {
                return string.Empty;
            }

            return ToLabel(abilityKeys[slot]);
        }

        /// <summary>Turns a KeyCode into something worth showing a player.</summary>
        private static string ToLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "Shift";
                case KeyCode.Space: return "Space";
                case KeyCode.Escape: return "Esc";
                default: return key.ToString();
            }
        }
    }
}
