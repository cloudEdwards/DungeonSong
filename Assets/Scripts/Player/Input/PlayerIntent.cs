using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// The direction the player is asking to attack in, derived from movement input at the
    /// moment the button was pressed. Combat maps this onto an actual attack.
    /// </summary>
    public enum AttackDirection
    {
        /// <summary>Whichever way the player faces.</summary>
        Forward = 0,

        Up,

        Down,

        DiagonalUpForward,

        DiagonalDownForward,

        /// <summary>Resolved by context rather than input: the player is on a wall.</summary>
        Wall,
    }

    /// <summary>
    /// A request to attack, captured at the instant of input.
    /// <para>
    /// The direction is recorded when the button is pressed, not when the attack finally
    /// runs. That distinction is what makes buffered input feel correct: pressing
    /// "up + attack" and then releasing up still produces an up attack.
    /// </para>
    /// </summary>
    public readonly struct AttackIntent
    {
        public readonly AttackDirection Direction;
        public readonly float TimeStamp;

        public AttackIntent(AttackDirection direction, float timeStamp)
        {
            Direction = direction;
            TimeStamp = timeStamp;
        }
    }

    /// <summary>A request to activate an equipped ability by loadout slot.</summary>
    public readonly struct AbilityIntent
    {
        public readonly int Slot;
        public readonly float TimeStamp;

        public AbilityIntent(int slot, float timeStamp)
        {
            Slot = slot;
            TimeStamp = timeStamp;
        }
    }

    /// <summary>
    /// Raw input, separated from meaning. <see cref="InputSystemSource"/> is the implementation;
    /// everything that reads controls — the router, PlayerController, the HUD's key labels —
    /// goes through this, so the bindings live in one actions asset.
    /// </summary>
    public interface IPlayerInputSource
    {
        /// <summary>Movement axis, -1..1 per component.</summary>
        Vector2 MoveAxis { get; }

        bool AttackPressed { get; }

        bool AttackHeld { get; }

        bool AttackReleased { get; }

        /// <summary>True on the frame the ability in <paramref name="slot"/> was pressed.</summary>
        bool AbilityPressed(int slot);

        /// <summary>Number of ability slots this source has bindings for.</summary>
        int AbilitySlotCount { get; }

        bool InteractPressed { get; }

        bool JumpPressed { get; }

        /// <summary>True on the frame jump was let go, for the variable-height jump.</summary>
        bool JumpReleased { get; }

        bool RollPressed { get; }

        bool BlockPressed { get; }

        bool BlockReleased { get; }

        /// <summary>
        /// What to show the player for an ability slot's control on the device they are using:
        /// "Q" on keyboard, "RT" on a pad. Empty when unbound.
        /// </summary>
        string GetAbilityKeyLabel(int slot);

        /// <summary>What to show the player for the interact control, e.g. "F".</summary>
        string InteractKeyLabel { get; }

        /// <summary>True once the player's last input came from a gamepad.</summary>
        bool UsingGamepad { get; }

        /// <summary>Raised when the player switches between keyboard/mouse and a gamepad, so labels can follow.</summary>
        event System.Action ControlsChanged;
    }
}
