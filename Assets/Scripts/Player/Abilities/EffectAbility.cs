namespace DungeonSong.Player
{
    /// <summary>
    /// Applies its definition's effects to whatever the targeting resolved to, and nothing
    /// else. This is the executor behind Cure Wounds.
    /// <para>
    /// It has no logic of its own on purpose: a self-heal, a touch-heal, a buff and a
    /// debuff are all this component with a different targeting mode and a different set of
    /// effects on the asset. Most future utility spells need no new executor at all.
    /// </para>
    /// </summary>
    public class EffectAbility : AbilityBehaviour
    {
        // Effects are applied by the base class immediately after OnResolve, so there is
        // genuinely nothing to do here.
        protected override void OnResolve(in TargetInfo target) { }
    }
}
