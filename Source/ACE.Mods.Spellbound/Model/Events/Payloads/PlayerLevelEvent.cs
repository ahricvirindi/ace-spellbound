using ACE.Mods.Spellbound.Model.Events;

namespace ACE.Mods.Spellbound.Model.Events.Payloads
{
    /// <summary>
    /// Fired once per level gained when <c>Player.CheckForLevelup</c> increments
    /// <c>Level</c>. A multi-level XP grant (49→52) fans out to three events
    /// (49→50, 50→51, 51→52) so per-level achievement rules ("reach level 50")
    /// don't need to reason about ranges or multi-level jumps.
    /// </summary>
    public sealed record PlayerLevelEvent(
        Player Leveler,
        int FromLevel,
        int ToLevel
    ) : SpellboundEventArgs
    {
        public override Player? Subject => Leveler;
    }
}
