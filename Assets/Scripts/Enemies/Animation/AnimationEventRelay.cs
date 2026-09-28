using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Bridges Animator clip events to the enemy's systems. Put this on whichever object
    /// holds the Animator and add events to the clips; the AI never learns that animation
    /// is involved.
    /// <para>
    /// This is the second of two ways to time an attack. The other is numeric phases on
    /// the <see cref="AttackDefinition"/>, which needs no clip authoring. Set the
    /// definition's timing source to AnimationEvents to hand control to these callbacks.
    /// </para>
    /// </summary>
    public class AnimationEventRelay : MonoBehaviour
    {
        [SerializeField, Tooltip("Enemy that receives the relayed events. Leave empty to search parents.")]
        private Enemy enemy;

        private void Awake()
        {
            if (enemy == null)
            {
                enemy = GetComponentInParent<Enemy>();
            }
        }

        /// <summary>Clip event: the attack's damaging frames begin now.</summary>
        public void AE_HitboxOn() => enemy?.Attacks?.NotifyAnimationHitboxOn();

        /// <summary>Clip event: the attack's damaging frames end now.</summary>
        public void AE_HitboxOff() => enemy?.Attacks?.NotifyAnimationHitboxOff();

        /// <summary>Clip event: recovery is over and the attack is finished.</summary>
        public void AE_AttackComplete() => enemy?.Attacks?.NotifyAnimationAttackComplete();

        /// <summary>Clip event: spawn this attack's projectile now.</summary>
        public void AE_SpawnProjectile() => enemy?.Attacks?.NotifyAnimationSpawnProjectile();

        /// <summary>Clip event: forwarded for footstep audio and dust.</summary>
        public void AE_Footstep() => enemy?.RaiseFootstep();
    }
}
