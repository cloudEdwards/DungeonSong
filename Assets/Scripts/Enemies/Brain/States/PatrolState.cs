using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Walks back and forth, turning at ledges, walls and optional patrol bounds. This is
    /// the whole behaviour of a simple walker, and the resting behaviour of complex enemies.
    /// </summary>
    public class PatrolState : EnemyStateBehaviour
    {
        [Header("Patrol")]
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of top speed used while patrolling.")]
        private float speedScale = 0.5f;

        [SerializeField, Tooltip("Turn around at ledges. Turn off for flyers and crawlers.")]
        private bool turnAtEdges = true;

        [SerializeField, Tooltip("Turn around at walls.")]
        private bool turnAtWalls = true;

        [Header("Bounds")]
        [SerializeField, Min(0f), Tooltip("Turn around this far from the spawn point. 0 disables bounds.")]
        private float patrolRadius;

        [Header("Pausing")]
        [SerializeField, Min(0f), Tooltip("Seconds to pause after each turn. Gives the player a read on the enemy.")]
        private float pauseAfterTurn = 0.4f;

        [Header("Presentation")]
        [SerializeField, Tooltip("Animation key played while walking.")]
        private string walkAnimationKey = "walk";

        private Vector2 origin;
        private float pauseTimer;
        private float turnCooldown;

        protected override int DefaultPriority => 10;

        /// <summary>Patrol whenever there is nothing to chase.</summary>
        public override bool WantsControl => !Owner.HasTarget;

        public override void OnEnemySpawned() => origin = transform.position;

        protected override void OnEnter()
        {
            pauseTimer = 0f;
            Anim.PlayAction(walkAnimationKey);
        }

        protected override void OnStateTick(float deltaTime)
        {
            if (Movement == null)
            {
                return;
            }

            if (turnCooldown > 0f)
            {
                turnCooldown -= deltaTime;
            }

            if (pauseTimer > 0f)
            {
                pauseTimer -= deltaTime;
                Movement.Stop();
                Anim.SetLocomotionSpeed(0f);
                return;
            }

            if (ShouldTurn())
            {
                Turn();
                return;
            }

            Movement.Move(new Vector2(Owner.FacingDirection, 0f), speedScale);
            Anim.SetLocomotionSpeed(speedScale);
        }

        private bool ShouldTurn()
        {
            if (turnCooldown > 0f)
            {
                return false;
            }

            if (turnAtWalls && Movement.IsBlockedAhead)
            {
                return true;
            }

            if (turnAtEdges && Movement.IsEdgeAhead)
            {
                return true;
            }

            if (patrolRadius > 0f)
            {
                float offset = transform.position.x - origin.x;
                if (Mathf.Abs(offset) > patrolRadius && Mathf.Sign(offset) == Owner.FacingDirection)
                {
                    return true;
                }
            }

            return false;
        }

        private void Turn()
        {
            // A crawler decides its own "around", so let the movement component turn itself.
            if (Movement is SurfaceMovement crawler)
            {
                crawler.ReverseTravel();
            }
            else
            {
                Owner.SetFacing(-Owner.FacingDirection);
            }

            Movement.Stop();
            pauseTimer = pauseAfterTurn;
            turnCooldown = Mathf.Max(0.1f, pauseAfterTurn);
        }
    }
}
