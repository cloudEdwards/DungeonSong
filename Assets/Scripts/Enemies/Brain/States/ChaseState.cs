using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Closes on the target and stops at a chosen distance, leaving the attack states to
    /// take over. Works for walkers and flyers alike: it expresses intent as a direction
    /// and lets the movement component decide what that means.
    /// </summary>
    public class ChaseState : EnemyStateBehaviour
    {
        [Header("Chase")]
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of top speed used while chasing.")]
        private float speedScale = 1f;

        [SerializeField, Min(0f), Tooltip("Stop closing once within this distance.")]
        private float stopDistance = 1.2f;

        [SerializeField, Tooltip("Chase in both axes. On for flyers, off for ground enemies.")]
        private bool verticalPursuit;

        [Header("Ledges")]
        [SerializeField, Tooltip("Refuse to walk off ledges while chasing. Ground enemies usually should.")]
        private bool stopAtEdges = true;

        [SerializeField, Tooltip("Jump when the target is above and a ledge blocks the way.")]
        private bool jumpAtLedges;

        [Header("Memory")]
        [SerializeField, Tooltip("Keep moving to the last known position after losing sight.")]
        private bool pursueLastKnownPosition = true;

        [Header("Presentation")]
        [SerializeField, Tooltip("Animation key played while chasing.")]
        private string chaseAnimationKey = "run";

        protected override int DefaultPriority => 30;

        public override bool WantsControl
        {
            get
            {
                if (!Owner.HasTarget)
                {
                    return false;
                }

                return DistanceToTarget > stopDistance;
            }
        }

        protected override void OnEnter() => Anim.PlayAction(chaseAnimationKey);

        protected override void OnStateTick(float deltaTime)
        {
            if (Movement == null)
            {
                return;
            }

            Vector2 destination = Target != null
                ? Target.AimPosition
                : pursueLastKnownPosition ? Owner.LastKnownTargetPosition : (Vector2)transform.position;

            Vector2 delta = destination - (Vector2)transform.position;

            if (!verticalPursuit)
            {
                delta.y = 0f;
            }

            if (delta.sqrMagnitude < 0.0004f)
            {
                Movement.Stop();
                Finish();
                return;
            }

            Owner.FaceTowards(destination);

            if (stopAtEdges && Movement.IsEdgeAhead)
            {
                if (jumpAtLedges && Movement.TryJump())
                {
                    return;
                }

                Movement.Stop();
                Anim.SetLocomotionSpeed(0f);
                return;
            }

            Movement.Move(delta, speedScale);
            Anim.SetLocomotionSpeed(speedScale);
        }

        protected override void OnExit()
        {
            Movement?.Stop();
            Anim.SetLocomotionSpeed(0f);
        }
    }
}
