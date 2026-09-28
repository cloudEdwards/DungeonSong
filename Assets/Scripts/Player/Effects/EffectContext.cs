using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>
    /// Everything an effect needs to know about the situation it is being applied in.
    /// <para>
    /// Passed by <c>in</c> so applying an effect allocates nothing, which matters once
    /// abilities start applying several effects per cast.
    /// </para>
    /// </summary>
    public readonly struct EffectContext
    {
        /// <summary>Who caused this. Used for damage attribution and friendly-fire rules.</summary>
        public readonly GameObject Source;

        /// <summary>Team of the source, so an effect can never hurt its own side by accident.</summary>
        public readonly DamageTeam SourceTeam;

        /// <summary>Where the effect happens.</summary>
        public readonly Vector2 Position;

        /// <summary>Direction the effect points, for knockback and projectiles.</summary>
        public readonly Vector2 Direction;

        /// <summary>The thing being affected. Null for effects that act on a position.</summary>
        public readonly GameObject Target;

        public EffectContext(GameObject source, DamageTeam sourceTeam, Vector2 position, Vector2 direction, GameObject target = null)
        {
            Source = source;
            SourceTeam = sourceTeam;
            Position = position;
            Direction = direction;
            Target = target;
        }
    }

    /// <summary>
    /// One reusable consequence: deal damage, heal, knock back, later apply a status.
    /// <para>
    /// Abilities are assembled from these rather than implementing consequences themselves,
    /// which is what lets "Eldritch Spear" reuse the damage effect that "Eldritch Blast"
    /// uses, and lets a future burn effect be dropped into either without touching them.
    /// </para>
    /// </summary>
    public abstract class GameplayEffect : ScriptableObject
    {
        [Header("Effect")]
        [Tooltip("Designer-facing description. Not used at runtime.")]
        [TextArea(1, 3)] public string Description;

        /// <summary>Applies this effect. Implementations must tolerate a null target.</summary>
        public abstract void Apply(in EffectContext context);
    }
}
