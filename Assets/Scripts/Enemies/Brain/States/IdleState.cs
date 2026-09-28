using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Stands still. Always willing to take control, which makes it the natural fallback
    /// when nothing else applies.
    /// </summary>
    public class IdleState : EnemyStateBehaviour
    {
        [Header("Idle")]
        [SerializeField, Tooltip("Animation key played on entering idle.")]
        private string idleAnimationKey = "idle";

        [SerializeField, Min(0f), Tooltip("Seconds to stay idle before finishing, letting a patrol resume. 0 stays idle indefinitely.")]
        private float duration;

        protected override int DefaultPriority => 0;

        public override bool WantsControl => true;

        protected override void OnEnter()
        {
            Movement?.Stop();
            Anim.SetLocomotionSpeed(0f);
            Anim.PlayAction(idleAnimationKey);
        }

        protected override void OnStateTick(float deltaTime)
        {
            if (duration > 0f && TimeInState >= duration)
            {
                Finish();
            }
        }
    }
}
