namespace ACE.Mods.Spellbound.Model.Events
{
    public abstract record SpellboundEventArgs
    {
        public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

        public abstract Player? Subject { get; }

        public uint? AccountId => Subject?.Account?.AccountId;
    }
}
