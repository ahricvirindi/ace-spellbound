using ACE.Mods.Spellbound.Model.Events;

namespace ACE.Mods.Spellbound.Model.Events.Payloads
{
    public sealed record PlayerDeathEvent(
        Player Victim,
        Creature? Killer,
        DamageType DamageType
    ) : SpellboundEventArgs
    {
        public override Player? Subject => Victim;
    }
}
