using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Puts an enemy to sleep when nothing is near it and wakes it when something is.
    /// <para>
    /// This is the main lever for holding many enemies in one scene: a dormant enemy runs
    /// no perception, no brain and no movement, and this check itself is one distance test
    /// a few times a second. It is also how ambushers work: leave
    /// <c>activateOnSpawn</c> off on the Enemy and let this wake it.
    /// </para>
    /// </summary>
    public class EnemyActivation : EnemyModule
    {
        [Header("Distances")]
        [SerializeField, Min(0f), Tooltip("Wake when a hostile target is closer than this.")]
        private float activationRadius = 14f;

        [SerializeField, Min(0f), Tooltip("Sleep again beyond this distance. Keep larger than the activation radius to avoid thrashing at the boundary.")]
        private float deactivationRadius = 20f;

        [Header("Behaviour")]
        [SerializeField, Tooltip("Sleep again once nothing is near. Off means it wakes once and stays awake.")]
        private bool canReturnToDormant = true;

        [SerializeField, Tooltip("Stay awake once it has a target, however far the target runs.")]
        private bool stayAwakeWhileEngaged = true;

        [Header("Performance")]
        [SerializeField, Min(0.05f), Tooltip("Seconds between checks. Four times a second is plenty for something this coarse.")]
        private float checkInterval = 0.25f;

        private float timer;

        /// <summary>This module is the reason dormant enemies still tick at all.</summary>
        public override bool TickWhileDormant => true;

        public override int TickOrder => ModuleTickOrder.Activation;

        public override void OnEnemySpawned()
        {
            // Spread the checks so enemies spawned together do not sample on one frame.
            timer = UnityEngine.Random.Range(0f, checkInterval);
        }

        public override void Tick(float deltaTime)
        {
            timer -= deltaTime;
            if (timer > 0f)
            {
                return;
            }

            timer = checkInterval;

            if (Owner.Lifecycle == EnemyLifecycleState.Dead || Owner.Lifecycle == EnemyLifecycleState.Despawned)
            {
                return;
            }

            if (stayAwakeWhileEngaged && Owner.HasTarget)
            {
                Owner.Activate();
                return;
            }

            ITargetable nearest = TargetRegistry.FindNearest(transform.position, Owner.HostileTeams, deactivationRadius);

            if (nearest == null)
            {
                if (canReturnToDormant)
                {
                    Owner.Deactivate();
                }

                return;
            }

            float distance = Vector2.Distance(transform.position, nearest.Transform.position);

            if (distance <= activationRadius)
            {
                Owner.Activate();
            }
            else if (canReturnToDormant && distance > deactivationRadius)
            {
                Owner.Deactivate();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, deactivationRadius);
        }
    }
}
