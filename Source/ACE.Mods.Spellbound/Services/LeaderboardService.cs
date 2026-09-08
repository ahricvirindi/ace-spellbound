using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Model.Enumerations;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Services
{
    // Atomic event-driven counter writes for the Leaderboards table. The
    // single sanctioned writer for everything in EventHandlers/LeaderboardRules/
    // and any future leaderboard producers.
    //
    // Why raw SQL: EF Core has no built-in upsert. The MySQL idiom
    // INSERT ... ON DUPLICATE KEY UPDATE is one round-trip and atomic at the
    // server, with no read-modify-write race that an EF
    // FirstOrDefault+SaveChanges path would expose. The unique index
    // (Category, Target, CharacterId) on the table is what makes the
    // duplicate-key trigger fire — see Updates/2026-04-30-006-add-leaderboards.sql.
    //
    // Why VALUES() instead of row aliases: VALUES() is supported all the
    // way back to MySQL 5.7. Row aliases (INSERT ... AS new ON DUPLICATE
    // KEY UPDATE col = new.col) only landed in 8.0.20. VALUES() shows a
    // deprecation warning on 8.0.20+ but still works; switch when 5.7
    // support is no longer relevant.
    public static class LeaderboardService
    {
        public static void RecordEvent(
            SpellboundContext db,
            LeaderboardCategory category,
            string? target,
            uint characterId,
            int accountId,
            string characterName,
            long increment = 1)
        {
            var categoryInt = (int)category;
            var safeTarget = target ?? string.Empty;
            var safeName = characterName ?? string.Empty;
            var now = DateTime.UtcNow;

            db.Database.ExecuteSqlInterpolated($@"
                INSERT INTO `Leaderboards`
                    (`Category`, `Target`, `CharacterId`, `AccountId`, `CharacterName`, `Count`, `UpdatedAt`)
                VALUES
                    ({categoryInt}, {safeTarget}, {characterId}, {accountId}, {safeName}, {increment}, {now})
                ON DUPLICATE KEY UPDATE
                    `Count` = `Count` + VALUES(`Count`),
                    `CharacterName` = VALUES(`CharacterName`),
                    `AccountId` = VALUES(`AccountId`),
                    `UpdatedAt` = VALUES(`UpdatedAt`)");
        }
    }
}
