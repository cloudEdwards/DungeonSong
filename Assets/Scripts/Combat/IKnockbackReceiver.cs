using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Implemented by whatever owns an actor's motion, so health code can push a
    /// receiver around without knowing whether it walks, flies or crawls on walls.
    /// </summary>
    public interface IKnockbackReceiver
    {
        /// <param name="direction">Normalized push direction.</param>
        /// <param name="force">Impulse strength in units/second.</param>
        /// <param name="controlLockSeconds">How long the actor's own movement stays suppressed.</param>
        void ApplyKnockback(Vector2 direction, float force, float controlLockSeconds);
    }
}
