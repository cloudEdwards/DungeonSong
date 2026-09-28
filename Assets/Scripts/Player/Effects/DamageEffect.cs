using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>
    /// Deals damage to the target through the shared damage pipeline, so defenses, poise
    /// and invulnerability all behave exactly as they do for a melee hit.
    /// </summary>
    [CreateAssetMenu(fileName = "DamageEffect", menuName = "Dungeon/Player/Effects/Damage")]
    public class DamageEffect : GameplayEffect
    {
        [Header("Damage")]
        public AttackPayload Payload = AttackPayload.Default;

        public override void Apply(in EffectContext context)
        {
            if (context.Target == null)
            {
                return;
            }

            var damageable = context.Target.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                return;
            }

            DamageInfo info = Payload.ToDamageInfo(context.SourceTeam, context.Position, context.Source);
            if (info.KnockbackForce > 0f)
            {
                info.AimKnockbackFrom(damageable.Transform.position, info.KnockbackForce, Payload.KnockbackUpwardBias);
            }

            DamageResult result = damageable.TakeDamage(in info);
            if (result.Applied)
            {
                HitStop.Request(Payload.HitStopSeconds);
            }
        }
    }
}
