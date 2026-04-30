namespace ACE.Mods.Spellbound.Model.Events.Payloads
{
    /// <summary>
    /// Fired right after the upstream cast pre-check returns a non-failed status.
    /// Subscribers may set <see cref="CancelCast"/> to abort the cast — the publisher
    /// reads the flag back after dispatch and translates it into
    /// <c>CastingPreCheckStatus.CastFailed</c> on the original <c>ref</c> result.
    ///
    /// Mutability: the positional record fields stay <c>init</c>-only (immutable
    /// snapshot of the inputs); the cancellation channel is the only mutable
    /// state. Subscribers must not mutate the caster, the spell, or any other
    /// gameplay state from this hook. If a rule wants to inform the player WHY the
    /// cast was cancelled, it should call <c>e.Caster.Tell(...)</c> directly — the
    /// publisher no longer relays a cancellation message for you.
    /// </summary>
    public sealed record PlayerPreCastEvent(
        Player Caster,
        Spell Spell,
        uint MagicSkill,
        bool IsWeaponSpell
    ) : SpellboundEventArgs
    {
        public override Player? Subject => Caster;

        public bool CancelCast { get; set; }
    }
}
