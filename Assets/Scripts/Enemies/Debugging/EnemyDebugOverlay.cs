using UnityEngine;

namespace DungeonSong.Enemies
{
    /// <summary>
    /// Draws what an enemy is thinking: current state, target, health and sensor status.
    /// AI is far easier to debug by looking at it than by reading it, so this is worth the
    /// one component.
    /// </summary>
    public class EnemyDebugOverlay : MonoBehaviour
    {
        [Header("What To Show")]
        [SerializeField, Tooltip("State name, target and health above the enemy (scene view only).")]
        private bool showStatusLabel = true;

        [SerializeField, Tooltip("Line to the current target.")]
        private bool showTargetLine = true;

        [SerializeField, Tooltip("Facing arrow and current velocity.")]
        private bool showMotion = true;

        [SerializeField, Tooltip("Attack ranges of every attack on this enemy.")]
        private bool showAttackRanges = true;

        [SerializeField, Tooltip("Show even when not selected. Noisy with many enemies.")]
        private bool alwaysVisible;

        [Header("Style")]
        [SerializeField] private Vector2 labelOffset = new Vector2(0f, 1.2f);

        private Enemy enemy;

        private void Awake() => enemy = GetComponent<Enemy>();

        private void OnDrawGizmos()
        {
            if (alwaysVisible)
            {
                Draw();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!alwaysVisible)
            {
                Draw();
            }
        }

        private void Draw()
        {
            if (enemy == null)
            {
                enemy = GetComponent<Enemy>();
                if (enemy == null)
                {
                    return;
                }
            }

            Vector3 position = transform.position;

            if (showTargetLine && enemy.CurrentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(position, enemy.CurrentTarget.AimPosition);
                Gizmos.DrawWireSphere(enemy.CurrentTarget.AimPosition, 0.2f);
            }

            if (showMotion)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawRay(position, new Vector3(enemy.FacingDirection * 0.8f, 0f, 0f));

                if (enemy.Movement != null)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(position, enemy.Movement.Velocity * 0.25f);
                }
            }

            if (showAttackRanges && enemy.Attacks != null)
            {
                var attacks = enemy.Attacks.Attacks;
                for (int i = 0; i < attacks.Count; i++)
                {
                    AttackDefinition definition = attacks[i].Definition;
                    if (definition == null)
                    {
                        continue;
                    }

                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
                    Gizmos.DrawWireSphere(position, definition.MaxRange);
                }
            }

#if UNITY_EDITOR
            if (showStatusLabel)
            {
                DrawLabel(position);
            }
#endif
        }

#if UNITY_EDITOR
        private void DrawLabel(Vector3 position)
        {
            string state = enemy.Brain != null ? enemy.Brain.CurrentStateName : "no brain";
            string health = enemy.Health != null ? $"{enemy.Health.Current:0}/{enemy.Health.Max:0}" : "-";
            string target = enemy.CurrentTarget != null ? enemy.CurrentTarget.Transform.name : "none";

            string attack = enemy.Attacks != null && enemy.Attacks.IsAttacking
                ? $"\nattack: {enemy.Attacks.CurrentAttack.Definition?.DisplayName} ({enemy.Attacks.CurrentAttack.Phase})"
                : string.Empty;

            string sensors = enemy.Movement != null
                ? $"\nground:{(enemy.Movement.IsGrounded ? 1 : 0)} wall:{(enemy.Movement.IsBlockedAhead ? 1 : 0)} edge:{(enemy.Movement.IsEdgeAhead ? 1 : 0)}"
                : string.Empty;

            UnityEditor.Handles.Label(
                position + (Vector3)labelOffset,
                $"{enemy.Lifecycle} | {state}\nhp: {health}  target: {target}{attack}{sensors}");
        }
#endif
    }
}
