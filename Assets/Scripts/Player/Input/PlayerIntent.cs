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
    /// Raw input, separated from meaning. Swapping the legacy Input API for the Input
    /// System package is a matter of writing a second implementation of this.
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

        bool InteractPressed { get; }
    }
}
