using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Keeps the target inside a preferred band: advances when too far, backs off when too
    /// close. The kiting behaviour shared by ranged enemies and cowardly melee ones.
    /// </summary>
    public class MaintainDistanceState : EnemyStateBehaviour
    {
        [Header("Band")]
        [SerializeField, Min(0f), Tooltip("Back away when the target is closer than this.")]
        private float minDistance = 3f;

        [SerializeField, Min(0f), Tooltip("Advance when the target is further than this.")]
        private float maxDistance = 5.5f;

        [Header("Movement")]
        [SerializeField, Range(0f, 1f)] private float speedScale = 0.8f;

        [SerializeField, Tooltip("Reposition vertically as well. On for flyers.")]
        private bool verticalPursuit = true;

        [SerializeField, Tooltip("Keep facing the target even while retreating.")]
        private bool alwaysFaceTarget = true;

        [Header("Presentation")]
        [SerializeField] private string moveAnimationKey = "fly";

        protected override int DefaultPriority => 30;

        public override bool WantsControl => Owner.HasTarget;

        /// <summary>True when the target sits inside the preferred band.</summary>
        public bool IsInBand
        {
            get
            {
                float distance = DistanceToTarget;
                return distance >= minDistance && distance <= maxDistance;
            }
        }

        protected override void OnEnter() => Anim.PlayAction(moveAnimationKey);

        protected override void OnStateTick(float deltaTime)
        {
            if (Movement == null || Target == null)
            {
                return;
            }

            Vector2 toTarget = Target.AimPosition - (Vector2)transform.position;
            float distance = toTarget.magnitude;

            if (alwaysFaceTarget)
            {
                Owner.FaceTowards(Target.AimPosition);
            }

            Vector2 steer;
            if (distance < minDistance)
            {
                steer = -toTarget;
            }
            else if (distance > maxDistance)
            {
                steer = toTarget;
            }
            else
            {
                // Inside the band: hold position so attack states can take over cleanly.
                Movement.Stop();
                Anim.SetLocomotionSpeed(0f);
                return;
            }

            if (!verticalPursuit)
            {
                steer.y = 0f;
            }

            Movement.Move(steer, speedScale);
            Anim.SetLocomotionSpeed(speedScale);
        }

        protected override void OnExit() => Movement?.Stop();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, minDistance);
            Gizmos.DrawWireSphere(transform.position, maxDistance);
        }
    }
}
