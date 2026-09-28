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
        private KeyCode[] abilityKeys = { KeyCode.F, KeyCode.Q, KeyCode.Alpha1, KeyCode.Alpha2 };

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
    }
}
