using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Refills the caster's resource pools that recover on a given kind of rest.
    /// <para>
    /// This plus a <see cref="HealEffect"/> is the whole short rest: the Warlock slots come
    /// back because their <see cref="ResourceDefinition.RefilledBy"/> includes
    /// <see cref="RestType.Short"/>, and Paladin slots stay spent because theirs does not.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "RestoreResourcesEffect", menuName = "Dungeon/Player/Effects/Restore Resources")]
    public class RestoreResourcesEffect : GameplayEffect
    {
        [Tooltip("Pools refilled are those whose RefilledBy includes this rest.")]
        public RestType Rest = RestType.Short;

        public override void Apply(in EffectContext context)
        {
            GameObject subject = context.Target != null ? context.Target : context.Source;
            if (subject == null)
            {
                return;
            }

            var pool = subject.GetComponentInParent<ResourcePool>();
            if (pool != null)
            {
                pool.RestoreFor(Rest);
            }
        }
    }
}
