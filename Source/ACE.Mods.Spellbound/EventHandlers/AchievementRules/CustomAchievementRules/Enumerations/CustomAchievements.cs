namespace ACE.Mods.Spellbound.EventHandlers.AchievementRules.CustomAchievementRules.Enumerations
{
    // Code-driven achievement ids. The numeric id MUST match the corresponding
    // row in `Database/Spellbound/Seeds/achievements.sql` — the
    // CustomAchievementRegistry passes the enum value to AwardById, so a
    // mismatch silently no-ops the award.
    //
    // Convention: 9000+ matches the data-driven seed range used elsewhere in
    // achievements.sql.
    public enum CustomAchievements
    {
        FIRST_CRIT_KILL = 9001
    }
}
