using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// The beat between noticing the player and reacting: stop, turn, play a tell. Small,
    /// but it is what makes an enemy feel like it saw you rather than teleporting into
    /// aggression, and it gives the player a reaction window.
    /// </summary>
    public class AlertState : EnemyStateBehaviour
    {
        [Header("Alert")]
        [SerializeField, Min(0f), Tooltip("Seconds spent reacting before pursuing.")]
        private float duration = 0.4f;

        [SerializeField, Tooltip("Turn to face the target on entering.")]
        private bool faceTarget = true;

        [SerializeField, Tooltip("Animation key played as the tell.")]
        private string alertAnimationKey = "alert";

        [Header("Noise")]
        [SerializeField, Min(0f), Tooltip("Radius of the noise emitted when alerted, so nearby enemies join in. 0 stays quiet.")]
        private float alertNoiseRadius;

        private bool pending;

        protected override int DefaultPriority => 55;

        public override bool WantsControl => pending;

        public override void OnEnemyInitialized() => Owner.TargetAcquired += OnTargetAcquired;

        private void OnDestroy()
        {
            if (Owner != null)
            {
                Owner.TargetAcquired -= OnTargetAcquired;
            }
        }

        public override void OnEnemySpawned() => pending = false;

        private void OnTargetAcquired(Enemy enemy, ITargetable target) => pending = true;

        protected override void OnEnter()
        {
            pending = false;
            Movement?.Stop();

            if (faceTarget)
            {
                Owner.FaceTarget();
            }

            Anim.SetLocomotionSpeed(0f);
            Anim.PlayAction(alertAnimationKey);

            if (alertNoiseRadius > 0f)
            {
                NoiseEvents.Emit(transform.position, alertNoiseRadius, Owner.Team);
            }
        }

        protected override void OnStateTick(float deltaTime)
        {
            if (TimeInState >= duration)
            {
                Finish();
            }
        }
    }
}
