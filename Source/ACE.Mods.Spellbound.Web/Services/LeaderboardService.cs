using ACE.Database.Models.Auth;
using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Model.Enumerations;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Web.Services;

// Read-only leaderboard composer. Each LeaderboardCategory has its own
// truth source — TotalXp is derived from CharacterProfileSnapshots while
// KillsByCreature reads the Leaderboards table written by the mod's
// PlayerOnKillLeaderboardHandler. The web app picks the right read path
// based on category, so the page doesn't have to know.
//
// Elevated-account filtering: every read excludes characters whose accounts
// have AccessLevel > Player (i.e. Advocate / Sentinel / Envoy / Developer /
// Admin). Mirrors the write-time skip in PlayerOnKillLeaderboardHandler —
// the table SHOULDN'T contain elevated rows in the first place, but the
// snapshot-derived leaderboards (TotalXp) write for everyone, and we want
// one consistent rule across all categories.
public sealed class LeaderboardService(
    SpellboundContext db,
    AuthDbContext authDb,
    ILogger<LeaderboardService> logger)
{
    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(
        LeaderboardCategory category,
        string target = "",
        int limit = 25)
    {
        try
        {
            // Resolve the elevated-account exclusion list once per query.
            // Cheap (O(few admin accounts)) and avoids cross-DB joins in
            // EF — Auth and Spellbound live in different DbContexts.
            var elevated = await authDb.Account
                .AsNoTracking()
                .Where(a => a.AccessLevel > 0)
                .Select(a => (int)a.AccountId)
                .ToListAsync();

            return category switch
            {
                LeaderboardCategory.TotalXp => await GetTopXpAsync(elevated, limit),
                LeaderboardCategory.KillsByCreature => await GetTopFromTableAsync(category, target, elevated, limit),
                _ => Array.Empty<LeaderboardEntry>()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Leaderboard query failed: category={Category} target={Target}", category, target);
            return Array.Empty<LeaderboardEntry>();
        }
    }

    // TotalXp is derived: read directly from the existing 30-min profile
    // snapshot. No event-driven hot path required, and the data is at most
    // 30 minutes stale — fine for a leaderboard.
    private async Task<IReadOnlyList<LeaderboardEntry>> GetTopXpAsync(
        IReadOnlyCollection<int> elevatedAccountIds, int limit) =>
        await db.CharacterProfileSnapshots
            .AsNoTracking()
            .Where(s => !elevatedAccountIds.Contains(s.AccountId))
            .OrderByDescending(s => s.TotalXp)
            .Take(limit)
            .Select(s => new LeaderboardEntry(s.CharacterId, s.CharacterName, s.TotalXp))
            .ToListAsync();

    private async Task<IReadOnlyList<LeaderboardEntry>> GetTopFromTableAsync(
        LeaderboardCategory category,
        string target,
        IReadOnlyCollection<int> elevatedAccountIds,
        int limit) =>
        await db.Leaderboards
            .AsNoTracking()
            .Where(l => l.Category == category && l.Target == target
                        && !elevatedAccountIds.Contains(l.AccountId))
            .OrderByDescending(l => l.Count)
            .Take(limit)
            .Select(l => new LeaderboardEntry(l.CharacterId, l.CharacterName, l.Count))
            .ToListAsync();
}

public sealed record LeaderboardEntry(uint CharacterId, string CharacterName, long Score);
