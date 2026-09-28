using UnityEngine;

namespace DungeonSong.Combat
{
    /// <summary>
    /// Anything that can receive damage: enemies, the player, breakable scenery.
    /// Hitboxes only ever talk to this, never to a concrete health class.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Team this actor fights for. Hitboxes filter on it.</summary>
        DamageTeam Team { get; }

        bool IsAlive { get; }

        /// <summary>Transform used for distance and direction maths.</summary>
        Transform Transform { get; }

        /// <summary>
        /// Runs the receiver's own defense and health pipeline. Implementations must
        /// tolerate being called while dead and return <see cref="DamageResult.Ignored"/>.
        /// </summary>
        DamageResult TakeDamage(in DamageInfo info);
    }
}
