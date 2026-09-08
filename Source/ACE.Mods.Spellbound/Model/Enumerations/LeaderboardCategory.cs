namespace ACE.Mods.Spellbound.Model.Enumerations
{
    // The controlled vocabulary for leaderboards. Each category names a single
    // axis the web app exposes. The READ source is category-specific:
    //
    //   * TotalXp         — derived. Read from CharacterProfileSnapshots
    //                       sorted by TotalXp DESC. No row in Leaderboards.
    //   * KillsByCreature — event-driven. Increments on Player_OnKill via
    //                       LeaderboardService.RecordKill. Target = the
    //                       creature type's enum name (e.g. "Tusker").
    //
    // To add a new category: append a new enum value, decide its read source
    // (derived or event-driven), and wire either the snapshot read in the web
    // LeaderboardService or a new handler under EventHandlers/LeaderboardRules/.
    // Ids are stable — never renumber, since they're persisted in the
    // Leaderboards.Category column.
    public enum LeaderboardCategory
    {
        Undef           = 0,
        TotalXp         = 1,
        KillsByCreature = 2,
    }
}
