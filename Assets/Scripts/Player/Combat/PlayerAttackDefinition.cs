using UnityEngine;
using DungeonSong.Combat;

namespace DungeonSong.Player
{
    /// <summary>Where an attack may be used, relative to the player's movement state.</summary>
    [System.Flags]
    public enum AttackContext
    {
        None = 0,
        Grounded = 1 << 0,
        Airborne = 1 << 1,
        OnWall = 1 << 2,
        Anywhere = Grounded | Airborne | OnWall,
    }

    /// <summary>What the player is allowed to do while an attack is running.</summary>
    [System.Flags]
    public enum AttackCancel
    {
        None = 0,

        /// <summary>Movement input still steers the player.</summary>
        Movement = 1 << 0,

        /// <summary>Jumping cuts the attack short.</summary>
        Jump = 1 << 1,

        /// <summary>A queued attack may cancel this one once its window opens.</summary>
        IntoAttack = 1 << 2,

        /// <summary>An ability may cancel this attack.</summary>
        IntoAbility = 1 << 3,
    }

    /// <summary>
    /// One player attack, as data. A forward slash, an up-thrust, a pogo, a wall strike, a
    /// charged holy smash are all this asset with different numbers and a different hitbox key.
    /// <para>
    /// This is the file a designer creates to add an attack. Nothing in code changes.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerAttack", menuName = "Dungeon/Player/Attack Definition")]
    public class PlayerAttackDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Attack";

        [Tooltip("Free-form tags for progression, upgrades and UI, e.g. 'nail', 'holy', 'heavy'.")]
        public string[] Tags = System.Array.Empty<string>();

        [Header("Selection")]
        [Tooltip("Direction this attack answers. The input router derives direction from input plus context.")]
        public AttackDirection Direction = AttackDirection.Forward;

        [Tooltip("Movement contexts this attack is legal in. An air-only pogo would be Airborne alone.")]
        public AttackContext AllowedContext = AttackContext.Anywhere;

        [Tooltip("Position in a combo chain. 0 is an opener; higher values are only reachable as follow-ups.")]
        [Min(0)] public int ComboIndex;

        [Tooltip("Higher wins when several attacks match the same direction and context.")]
        public int Priority;

        [Header("Timing")]
        [Min(0f), Tooltip("Wind-up before the hitbox opens. This is the tell; keep it non-zero.")]
        public float Startup = 0.08f;

        [Min(0f), Tooltip("Seconds the hitbox stays live.")]
        public float Active = 0.1f;

        [Min(0f), Tooltip("Seconds after the active frames before the player is free again.")]
        public float Recovery = 0.16f;

        [Min(0f), Tooltip("Seconds before this attack can be used again. 0 for basic strikes.")]
        public float Cooldown;

        [Header("Combo")]
        [Tooltip("Attack to chain into when the player attacks again during the combo window.")]
        public PlayerAttackDefinition FollowUp;

        [Min(0f), Tooltip("Seconds after the active frames during which a follow-up may be queued.")]
        public float ComboWindow = 0.35f;

        [Header("Damage")]
        public AttackPayload Payload = AttackPayload.Default;

        [Header("Hitbox")]
        [Tooltip("Key of the hitbox to open, as registered in the player's HitboxDirectory.")]
        public string HitboxKey = "Forward";

        [Min(0), Tooltip("Maximum victims per swing. 0 = unlimited.")]
        public int MaxTargets;

        [Header("Self Motion")]
        [Tooltip("Root the player for the duration, for heavy committed attacks.")]
        public bool LockMovement;

        [Tooltip("Velocity applied to the player when the hitbox opens. A downward air attack that connects uses RecoilOnHit instead.")]
        public Vector2 SelfVelocity;

        [Tooltip("Upward velocity applied when this attack connects with something. This is the pogo bounce.")]
        [Min(0f)] public float RecoilOnHit;

        [Tooltip("Only apply the recoil while airborne, so a grounded down-strike does not launch the player.")]
        public bool RecoilRequiresAirborne = true;

        [Tooltip("Seconds of invulnerability granted when the recoil fires, so bouncing off an enemy is not punished by its contact damage.")]
        [Min(0f)] public float InvulnerabilityOnRecoil;

        [Header("Interrupts")]
        [Tooltip("What the player may do while this attack runs.")]
        public AttackCancel CancelRules = AttackCancel.Movement | AttackCancel.Jump | AttackCancel.IntoAttack;

        [Header("Cost")]
        [Tooltip("Optional resource cost. Leave the resource empty for a free attack.")]
        public ResourceCost Cost;

        [Header("Presentation")]
        [Tooltip("Semantic animation key, resolved by the player's animator adapter.")]
        public string AnimationKey = "attack_forward";

        /// <summary>Total time from start to the end of recovery.</summary>
        public float TotalDuration => Startup + Active + Recovery;

        /// <summary>True when this attack is legal in the supplied movement context.</summary>
        public bool IsAllowedIn(AttackContext context) => (AllowedContext & context) != 0;
    }
}
