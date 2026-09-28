using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Investigates where the target was last seen, then gives up. Without this, enemies
    /// snap from hunting to oblivious the instant they lose sight, which reads as stupid.
    /// </summary>
    public class SearchState : EnemyStateBehaviour
    {
        [Header("Search")]
        [SerializeField, Min(0f), Tooltip("Seconds spent searching before giving up.")]
        private float duration = 2.5f;

        [SerializeField, Range(0f, 1f)] private float speedScale = 0.6f;

        [SerializeField, Min(0f), Tooltip("Stop this far from the last known position.")]
        private float arriveDistance = 0.5f;

        [SerializeField, Tooltip("Turn around a few times on arriving, to sell the search.")]
        private bool lookAround = true;

        [SerializeField, Min(0.1f), Tooltip("Seconds between look-around turns.")]
        private float lookInterval = 0.7f;

        [Header("Presentation")]
        [SerializeField] private string searchAnimationKey = "walk";

        private bool arrived;
        private float lookTimer;

        protected override int DefaultPriority => 25;

        /// <summary>Search only while perception still remembers something.</summary>
        public override bool WantsControl => !Owner.HasTarget && Perception != null && Perception.IsSearching;

        protected override void OnEnter()
        {
            arrived = false;
            lookTimer = lookInterval;
            Anim.PlayAction(searchAnimationKey);
        }

        protected override void OnStateTick(float deltaTime)
        {
            if (Movement == null)
            {
                Finish();
                return;
            }

            if (TimeInState >= duration)
            {
                Perception?.Clear();
                Finish();
                return;
            }

            Vector2 destination = Owner.LastKnownTargetPosition;
            float dx = destination.x - transform.position.x;

            if (!arrived && Mathf.Abs(dx) > arriveDistance && !Movement.IsEdgeAhead && !Movement.IsBlockedAhead)
            {
                Owner.FaceTowards(destination);
                Movement.Move(new Vector2(Mathf.Sign(dx), 0f), speedScale);
                Anim.SetLocomotionSpeed(speedScale);
                return;
            }

            arrived = true;
            Movement.Stop();
            Anim.SetLocomotionSpeed(0f);

            if (!lookAround)
            {
                return;
            }

            lookTimer -= deltaTime;
            if (lookTimer <= 0f)
            {
                lookTimer = lookInterval;
                Owner.SetFacing(-Owner.FacingDirection);
            }
        }
    }
}
