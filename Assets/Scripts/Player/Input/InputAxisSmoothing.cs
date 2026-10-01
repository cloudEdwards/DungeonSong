using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// The ramp the legacy Input Manager put on a keyboard axis (<c>Input.GetAxis</c>),
    /// reproduced for the Input System, which reports raw values. Running speed is
    /// multiplied by this, so it is what gives movement its short acceleration and slide.
    /// <para>
    /// Defaults match the old "Horizontal" axis: sensitivity 3, gravity 3, snap on.
    /// </para>
    /// </summary>
    public static class InputAxisSmoothing
    {
        public const float DefaultSensitivity = 3f;
        public const float DefaultGravity = 3f;

        /// <summary>
        /// Advances <paramref name="current"/> toward <paramref name="target"/>: up at
        /// <paramref name="sensitivity"/> units per second while held, back to zero at
        /// <paramref name="gravity"/> when released. With <paramref name="snap"/>, pressing the
        /// opposite direction jumps through zero instead of easing across it.
        /// </summary>
        public static float Step(
            float current,
            float target,
            float deltaTime,
            float sensitivity = DefaultSensitivity,
            float gravity = DefaultGravity,
            bool snap = true)
        {
            if (Mathf.Approximately(target, 0f))
            {
                return Mathf.MoveTowards(current, 0f, gravity * deltaTime);
            }

            if (snap && current * target < 0f)
            {
                current = 0f;
            }

            return Mathf.MoveTowards(current, target, sensitivity * deltaTime);
        }
    }
}
