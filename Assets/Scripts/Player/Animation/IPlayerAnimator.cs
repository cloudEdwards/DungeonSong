namespace DungeonSong.Player
{
    /// <summary>
    /// The only thing combat and abilities are allowed to know about animation.
    /// <para>
    /// Calls are semantic ("play the action called attack_up"), never Animator parameter
    /// names, so re-rigging the player is a change to its bindings rather than to every
    /// attack and spell. This is the reason no ability ever calls
    /// <c>animator.SetTrigger</c> directly.
    /// </para>
    /// </summary>
    public interface IPlayerAnimator
    {
        /// <summary>Fires a one-shot action, e.g. "attack_forward", "cast", "hurt".</summary>
        void PlayAction(string actionKey);

        /// <summary>Sets a sustained boolean, e.g. "blocking", "resting".</summary>
        void SetFlag(string flagKey, bool value);

        /// <summary>Scales playback speed, for charge-ups and haste effects.</summary>
        void SetSpeedMultiplier(float multiplier);

        /// <summary>True when the named action is currently playing, where the rig can report it.</summary>
        bool IsPlaying(string actionKey);
    }

    /// <summary>
    /// Stand-in used when no animator is wired, so blockouts and tests run without null
    /// checks scattered through every ability.
    /// </summary>
    public sealed class NullPlayerAnimator : IPlayerAnimator
    {
        public static readonly NullPlayerAnimator Instance = new NullPlayerAnimator();

        private NullPlayerAnimator() { }

        public void PlayAction(string actionKey) { }

        public void SetFlag(string flagKey, bool value) { }

        public void SetSpeedMultiplier(float multiplier) { }

        public bool IsPlaying(string actionKey) => false;
    }
}
