using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Runs away, either because it is badly hurt or because something told it to. Also
    /// serves as the retreat half of hit-and-run enemies.
    /// </summary>
    public class FleeState : EnemyStateBehaviour
    {
        [Header("Trigger")]
        [SerializeField, Range(0f, 1f), Tooltip("Flee below this fraction of max health. 0 never flees on its own.")]
        private float healthThreshold = 0.25f;

        [SerializeField, Tooltip("Only flee while a target is present.")]
        private bool requireTarget = true;

        [Header("Flee")]
        [SerializeField, Range(0f, 1f)] private float speedScale = 1f;

        [SerializeField, Min(0f), Tooltip("Stop fleeing once this far from the threat. 0 flees for the whole duration.")]
        private float safeDistance = 8f;

        [SerializeField, Min(0f), Tooltip("Maximum seconds spent fleeing.")]
        private float maxDuration = 3f;

        [SerializeField, Tooltip("Flee vertically too. On for flyers.")]
        private bool verticalFlee;

        [Header("Presentation")]
        [SerializeField] private string fleeAnimationKey = "run";

        private bool forced;

        protected override int DefaultPriority => 70;

        public override bool WantsControl
        {
            get
            {
                if (forced)
                {
                    return true;
                }

                if (healthThreshold <= 0f || Health == null)
                {
                    return false;
                }

                if (requireTarget && !Owner.HasTarget)
                {
                    return false;
                }

                return Health.Normalized <= healthThreshold;
            }
        }

        /// <summary>Makes the enemy flee regardless of health, e.g. on a boss phase change.</summary>
        public void ForceFlee() => forced = true;

        protected override void OnEnter() => Anim.PlayAction(fleeAnimationKey);

        protected override void OnStateTick(float deltaTime)
        {
            if (Movement == null || TimeInState >= maxDuration)
            {
                forced = false;
                Finish();
                return;
            }

            Vector2 threat = Target != null ? Target.AimPosition : Owner.LastKnownTargetPosition;
            Vector2 away = (Vector2)transform.position - threat;

            if (safeDistance > 0f && away.magnitude >= safeDistance)
            {
                forced = false;
                Finish();
                return;
            }

            if (!verticalFlee)
            {
                away.y = 0f;
            }

            // Cornered: stop rather than grinding into the wall, so the player can finish it.
            if (Movement.IsBlockedAhead && !verticalFlee)
            {
                Movement.Stop();
                Anim.SetLocomotionSpeed(0f);
                return;
            }

            Owner.FaceTowards(threat);
            Movement.Move(away, speedScale);
            Anim.SetLocomotionSpeed(speedScale);
        }

        protected override void OnExit()
        {
            forced = false;
            Movement?.Stop();
        }
    }
}
