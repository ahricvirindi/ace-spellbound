using ACE.Mods.Spellbound.Model.Events;

namespace ACE.Mods.Spellbound.Model.Events.Payloads
{
    public sealed record PlayerKillEvent(
        Player Killer,
        Creature Victim,
        DamageType DamageType,
        bool CriticalHit
    ) : SpellboundEventArgs
    {
        public override Player? Subject => Killer;
    }
}
