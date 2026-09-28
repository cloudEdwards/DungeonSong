using UnityEngine;

namespace DungeonSong.Player
{
    /// <summary>
    /// Shows what the player's combat and ability systems are doing.
    /// <para>
    /// Most "why didn't my attack come out?" questions are answered by one of these lines:
    /// the wrong movement context, an unmatched direction, a cooldown, or an unaffordable
    /// cost. Reading that off the screen beats reading it out of the code.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerActor))]
    public class PlayerDebugOverlay : MonoBehaviour
    {
        [Header("What To Show")]
        [SerializeField] private bool showCombat = true;

        [SerializeField] private bool showAbilities = true;

        [SerializeField] private bool showResources = true;

        [SerializeField] private bool showInteraction = true;

        [SerializeField] private bool showCheckpoint = true;

        [Header("Style")]
        [SerializeField] private Vector2 labelOffset = new Vector2(0f, 1.6f);

        private PlayerActor player;
        private PlayerCombat combat;
        private AbilityLoadout abilities;
        private ResourcePool resources;
        private PlayerInteractor interactor;

        private void Awake() => Resolve();

        private void Resolve()
        {
            player = GetComponent<PlayerActor>();
            combat = GetComponent<PlayerCombat>();
            abilities = GetComponent<AbilityLoadout>();
            resources = GetComponent<ResourcePool>();
            interactor = GetComponent<PlayerInteractor>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (player == null)
            {
                Resolve();
                if (player == null)
                {
                    return;
                }
            }

            var text = new System.Text.StringBuilder();

            float hp = player.Health?.Current ?? 0f;
            float maxHp = player.Health?.Max ?? 0f;
            text.Append($"HP {hp:0}/{maxHp:0}");

            if (player.Health != null && player.Health.IsInvulnerable)
            {
                text.Append("  [i-frames]");
            }

            if (showCombat && combat != null)
            {
                text.Append($"\ncontext: {combat.CurrentContext()}");
                text.Append(combat.IsAttacking
                    ? $"\nattack: {combat.CurrentAttack.DisplayName} ({combat.Phase})"
                    : "\nattack: none");

                if (combat.IsComboWindowOpen)
                {
                    text.Append("  [combo window]");
                }
            }

            if (showAbilities && abilities != null)
            {
                for (int i = 0; i < abilities.Slots.Count; i++)
                {
                    AbilityBehaviour ability = abilities.Slots[i];
                    if (ability == null || ability.Definition == null)
                    {
                        continue;
                    }

                    text.Append($"\n[{i}] {ability.Definition.DisplayName}");

                    if (ability.CooldownRemaining > 0f)
                    {
                        text.Append($" cd {ability.CooldownRemaining:0.0}s");
                    }
                    else if (!ability.CanActivate())
                    {
                        text.Append(" (cannot cast)");
                    }

                    if (ability.ChargesRemaining >= 0)
                    {
                        text.Append($" x{ability.ChargesRemaining}");
                    }
                }
            }

            if (showResources && resources != null)
            {
                for (int i = 0; i < resources.Resources.Count; i++)
                {
                    ResourceDefinition definition = resources.Resources[i];
                    if (definition != null)
                    {
                        text.Append($"\n{definition.DisplayName}: {resources.GetAmount(definition):0}/{definition.MaxAmount:0}");
                    }
                }
            }

            if (showInteraction && interactor != null && interactor.Available != null)
            {
                text.Append($"\ninteract: {interactor.Available.InteractionPrompt}");
            }

            if (showCheckpoint)
            {
                text.Append($"\ncheckpoint: {DebugCheckpointLabel()}");
            }

            UnityEditor.Handles.Label(transform.position + (Vector3)labelOffset, text.ToString());
        }

        /// <summary>
        /// Read reflectively so the player assembly does not have to depend on the world
        /// assembly just to print a debug line.
        /// </summary>
        private static string DebugCheckpointLabel()
        {
            System.Type type = System.Type.GetType("DungeonSong.World.CheckpointService, DungeonSong.World");
            if (type == null)
            {
                return "n/a";
            }

            System.Reflection.PropertyInfo property = type.GetProperty("Current");
            object state = property?.GetValue(null);
            if (state == null)
            {
                return "none";
            }

            System.Reflection.FieldInfo id = state.GetType().GetField("RestPointId");
            string value = id?.GetValue(state) as string;
            return string.IsNullOrEmpty(value) ? "none" : value;
        }
#endif
    }
}
