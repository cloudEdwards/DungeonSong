using DungeonSong.Combat;

namespace DungeonSong.Enemies
{
    /// <summary>Phases of a running attack.</summary>
    public enum AttackPhase
    {
        /// <summary>Not running.</summary>
        Ready = 0,

        /// <summary>Winding up. Nothing can be hit yet.</summary>
        Startup,

        /// <summary>Hitbox live.</summary>
        Active,

        /// <summary>Committed and vulnerable.</summary>
        Recovery,
    }

    /// <summary>
    /// One attack an enemy can perform. Implementations own only what makes their attack
    /// different (a hitbox, a projectile); phase timing, cooldown and usage conditions are
    /// shared by <see cref="AttackBehaviour"/>.
    /// </summary>
    public interface IAttack
    {
        AttackDefinition Definition { get; }

        AttackPhase Phase { get; }

        /// <summary>Off cooldown and not already running.</summary>
        bool IsReady { get; }

        bool IsRunning { get; }

        /// <summary>Range, facing, line of sight and any subclass-specific conditions.</summary>
        bool CanUse(ITargetable target);

        /// <summary>Starts the attack. Only call when <see cref="CanUse"/> passed.</summary>
        void Begin(ITargetable target);

        /// <summary>Aborts mid-swing and makes safe (hitboxes off).</summary>
        void Cancel();
    }
}
