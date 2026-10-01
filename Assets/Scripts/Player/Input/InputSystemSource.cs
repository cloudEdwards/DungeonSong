using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonSong.Player
{
    /// <summary>
    /// Reads the player's controls from an Input System actions asset: by default the
    /// project-wide one (<c>Assets/InputSystem_Actions</c>, Project Settings ▸ Input System
    /// Package). Rebinding is an edit to that asset, and the HUD's key labels follow it.
    /// <para>
    /// This is the only file in the player framework that touches a concrete input API.
    /// Actions are looked up by name, once, so a renamed action fails loudly here rather
    /// than silently doing nothing.
    /// </para>
    /// </summary>
    public class InputSystemSource : MonoBehaviour, IPlayerInputSource
    {
        private const string KeyboardMouseGroup = "Keyboard&Mouse";
        private const string GamepadGroup = "Gamepad";

        [Header("Actions")]
        [SerializeField, Tooltip("Actions to read. Leave empty to use the project-wide actions.")]
        private InputActionAsset actions;

        [SerializeField, Tooltip("Action map holding the player's controls.")]
        private string mapName = "Player";

        [SerializeField, Tooltip("Actions for the ability slots, in slot order.")]
        private string[] abilityActionNames = { "Ability1", "Ability2", "Ability3", "Ability4", "Ability5" };

        private InputActionMap map;
        private InputAction move;
        private InputAction attack;
        private InputAction block;
        private InputAction jump;
        private InputAction roll;
        private InputAction interact;
        private InputAction[] abilities;

        /// <summary>Raised when the player switches between keyboard/mouse and a gamepad.</summary>
        public event System.Action ControlsChanged;

        /// <summary>True once the player's last input came from a gamepad.</summary>
        public bool UsingGamepad { get; private set; }

        private void Awake() => Resolve();

        private void OnEnable() => Resolve()?.Enable();

        private void OnDestroy()
        {
            if (map != null)
            {
                map.actionTriggered -= OnActionTriggered;
            }
        }

        // Whichever device drove the last action decides which bindings the HUD shows.
        private void OnActionTriggered(InputAction.CallbackContext context)
        {
            if (context.control == null)
            {
                return;
            }

            bool gamepad = context.control.device is Gamepad;
            if (gamepad != UsingGamepad)
            {
                UsingGamepad = gamepad;
                ControlsChanged?.Invoke();
            }
        }

        public Vector2 MoveAxis => Resolve() != null ? move.ReadValue<Vector2>() : Vector2.zero;

        public bool AttackPressed => Resolve() != null && attack.WasPressedThisFrame();

        public bool AttackHeld => Resolve() != null && attack.IsPressed();

        public bool AttackReleased => Resolve() != null && attack.WasReleasedThisFrame();

        public bool InteractPressed => Resolve() != null && interact.WasPressedThisFrame();

        public bool JumpPressed => Resolve() != null && jump.WasPressedThisFrame();

        public bool JumpReleased => Resolve() != null && jump.WasReleasedThisFrame();

        public bool RollPressed => Resolve() != null && roll.WasPressedThisFrame();

        public bool BlockPressed => Resolve() != null && block.WasPressedThisFrame();

        public bool BlockReleased => Resolve() != null && block.WasReleasedThisFrame();

        public int AbilitySlotCount => Resolve() != null ? abilities.Length : 0;

        public bool AbilityPressed(int slot)
        {
            return Resolve() != null && slot >= 0 && slot < abilities.Length && abilities[slot].WasPressedThisFrame();
        }

        public string GetAbilityKeyLabel(int slot)
        {
            return Resolve() != null && slot >= 0 && slot < abilities.Length ? Label(abilities[slot]) : string.Empty;
        }

        public string InteractKeyLabel => Resolve() != null ? Label(interact) : string.Empty;

        /// <summary>
        /// Finds the actions. Lazy as well as in Awake, so the source works in edit-mode tests
        /// where Awake never runs. Returns null, once warned, when the map is missing.
        /// </summary>
        private InputActionMap Resolve()
        {
            if (map != null)
            {
                return map;
            }

            InputActionAsset asset = actions != null ? actions : InputSystem.actions;
            map = asset != null ? asset.FindActionMap(mapName) : null;
            if (map == null)
            {
                Debug.LogError($"InputSystemSource on '{name}' found no '{mapName}' action map. Assign an actions asset or set the project-wide actions.", this);
                return null;
            }

            move = Require("Move");
            attack = Require("Attack");
            block = Require("Block");
            jump = Require("Jump");
            roll = Require("Roll");
            interact = Require("Interact");

            abilities = new InputAction[abilityActionNames.Length];
            for (int i = 0; i < abilities.Length; i++)
            {
                abilities[i] = Require(abilityActionNames[i]);
            }

            map.actionTriggered += OnActionTriggered;
            return map;
        }

        private InputAction Require(string actionName)
        {
            // throwIfNotFound: a missing action is a broken asset, not a key the player never presses.
            return map.FindAction(actionName, throwIfNotFound: true);
        }

        /// <summary>
        /// The binding for the device in use, as a player would read it ("Q", "RT"), falling
        /// back to the other device when this one has no binding for the action.
        /// </summary>
        private string Label(InputAction action)
        {
            string preferred = UsingGamepad ? GamepadGroup : KeyboardMouseGroup;
            string other = UsingGamepad ? KeyboardMouseGroup : GamepadGroup;

            string label = action.GetBindingDisplayString(InputBinding.MaskByGroup(preferred));
            if (string.IsNullOrEmpty(label))
            {
                label = action.GetBindingDisplayString(InputBinding.MaskByGroup(other));
            }

            return Shorten(label);
        }

        // "D-Pad Down" (or "D-Pad/Down" with no pad connected) does not fit an ability slot;
        // an arrow does.
        private static string Shorten(string label)
        {
            if (string.IsNullOrEmpty(label) || !label.StartsWith("D-Pad"))
            {
                return label;
            }

            string direction = label.Substring(label.LastIndexOfAny(new[] { ' ', '/' }) + 1);
            switch (direction)
            {
                case "Up": return "D\u2191";
                case "Down": return "D\u2193";
                case "Left": return "D\u2190";
                case "Right": return "D\u2192";
                default: return label;
            }
        }
    }
}
