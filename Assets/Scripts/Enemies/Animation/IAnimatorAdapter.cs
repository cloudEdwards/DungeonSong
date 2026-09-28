namespace DungeonSong.Enemies
{
    /// <summary>
    /// The only thing AI, movement and combat are allowed to know about animation.
    /// Calls are semantic ("play the action called attack_slash"), never Animator
    /// parameter names, so re-rigging an enemy is a change to its bindings and not to
    /// its behaviour code.
    /// </summary>
    public interface IAnimatorAdapter
    {
        /// <summary>Horizontal movement speed, normalized 0..1 against the enemy's top speed.</summary>
        void SetLocomotionSpeed(float normalizedSpeed);

        /// <summary>Signed vertical speed, for fall and rise blends.</summary>
        void SetVerticalSpeed(float verticalSpeed);

        void SetGrounded(bool grounded);

        /// <summary>Fires a one-shot action, e.g. "attack_slash", "hurt", "death".</summary>
        void PlayAction(string actionKey);

        /// <summary>Sets a sustained boolean, e.g. "shielding", "burrowed".</summary>
        void SetFlag(string flagKey, bool value);

        /// <summary>Scales playback speed, for charge-ups and enrage phases.</summary>
        void SetSpeedMultiplier(float multiplier);
    }

    /// <summary>
    /// Stand-in used when an enemy has no animator wired. Lets blockouts and tests run
    /// without null checks scattered through every caller.
    /// </summary>
    public sealed class NullAnimatorAdapter : IAnimatorAdapter
    {
        public static readonly NullAnimatorAdapter Instance = new NullAnimatorAdapter();

        private NullAnimatorAdapter() { }

        public void SetLocomotionSpeed(float normalizedSpeed) { }

        public void SetVerticalSpeed(float verticalSpeed) { }

        public void SetGrounded(bool grounded) { }

        public void PlayAction(string actionKey) { }

        public void SetFlag(string flagKey, bool value) { }

        public void SetSpeedMultiplier(float multiplier) { }
    }
}
