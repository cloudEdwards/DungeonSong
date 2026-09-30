namespace DungeonSong.Combat
{
    /// <summary>Handler for <see cref="CombatEvents.DamageDealt"/>.</summary>
    public delegate void DamageDealtHandler(in DamageInfo info, in DamageResult result, Hurtbox victim);

    /// <summary>
    /// A global channel announcing every hit that reached a hurtbox, whoever dealt it.
    /// <para>
    /// Raised from <see cref="Hurtbox.Receive"/>, the single point every melee swing,
    /// projectile, cone and hazard passes through. That is what lets "gain Loyalty when you
    /// hit something" work for every attack without any attack knowing about Loyalty.
    /// Filter on <see cref="DamageInfo.Attacker"/> to hear only your own hits.
    /// </para>
    /// </summary>
    public static class CombatEvents
    {
        public static event DamageDealtHandler DamageDealt;

        public static void RaiseDamageDealt(in DamageInfo info, in DamageResult result, Hurtbox victim)
        {
            DamageDealt?.Invoke(in info, in result, victim);
        }
    }
}
