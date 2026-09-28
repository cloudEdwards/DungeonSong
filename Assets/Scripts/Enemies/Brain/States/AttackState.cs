using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Hands control to the <see cref="AttackController"/> and holds it until the swing is
    /// over. This state contains no attack logic of its own, which is why a new attack is a
    /// new component plus an asset, never a new state.
    /// </summary>
    public class AttackState : EnemyStateBehaviour
    {
        [Header("Attack")]
        [SerializeField, Tooltip("Face the target when the attack begins.")]
        private bool faceTargetOnEnter = true;

        [SerializeField, Tooltip("Hold position for the whole attack. Off lets attacks move the enemy (lunges, leaps).")]
        private bool stopWhileAttacking = true;

        [SerializeField, Tooltip("Let higher-priority states interrupt mid-attack. Off makes every attack committed; most enemies want the attack's own definition to decide, which this defers to.")]
        private bool deferInterruptibilityToAttack = true;

        protected override int DefaultPriority => 60;

        /// <summary>Attack when the controller reports something usable against the target.</summary>
        public override bool WantsControl => Owner.HasTarget && Attacks != null && Attacks.HasUsableAttack(Target);

        public override bool CanEnter => Attacks != null;

        public override bool IsInterruptible
        {
            get
            {
                if (Attacks == null || !Attacks.IsAttacking)
                {
                    return true;
                }

                return deferInterruptibilityToAttack ? Attacks.IsCurrentAttackInterruptible : false;
            }
        }

        protected override void OnEnter()
        {
            if (faceTargetOnEnter)
            {
                Owner.FaceTarget();
            }

            if (stopWhileAttacking)
            {
                Movement?.Stop();
                Anim.SetLocomotionSpeed(0f);
            }

            if (Attacks == null || !Attacks.TryBeginAttack(Target))
            {
                Finish();
            }
        }

        protected override void OnStateTick(float deltaTime)
        {
            if (Attacks == null || !Attacks.IsAttacking)
            {
                Finish();
            }
        }

        protected override void OnExit()
        {
            // Leaving mid-swing (staggered, killed) must not leave a hitbox live.
            if (Attacks != null && Attacks.IsAttacking)
            {
                Attacks.CancelCurrentAttack();
            }
        }
    }
}
