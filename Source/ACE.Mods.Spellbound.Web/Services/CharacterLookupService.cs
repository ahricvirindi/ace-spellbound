using ACE.Database.Models.Shard;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Web.Services;

// Read-only character lookups against ace_shard. The shard is the source of
// truth for character identity (Id, Name, AccountId, IsDeleted). Snapshot
// tables in ace_mod_spellbound only cover characters that have logged in
// since the snapshot system was deployed, so name-search has to hit shard.
//
// Profile rendering reads from snapshots, but resolution of "name -> id" runs
// here — see CharacterProfileService.GetByNameAsync.
public sealed class CharacterLookupService(
    ShardDbContext shardDb,
    ILogger<CharacterLookupService> logger)
{
    public async Task<IReadOnlyList<CharacterSearchResult>> SearchAsync(string query, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return Array.Empty<CharacterSearchResult>();

        var pattern = $"%{query.Trim()}%";

        try
        {
            // utf8 collation is case-insensitive, so LIKE matches in either case.
            return await shardDb.Character
                .AsNoTracking()
                .Where(c => !c.IsDeleted && EF.Functions.Like(c.Name, pattern))
                .OrderBy(c => c.Name)
                .Take(limit)
                .Select(c => new CharacterSearchResult(c.Id, c.Name, c.AccountId))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Character search failed for query={Query}", query);
            return Array.Empty<CharacterSearchResult>();
        }
    }

    public async Task<CharacterIdentity?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        try
        {
            return await shardDb.Character
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.Name == name)
                .Select(c => new CharacterIdentity(c.Id, c.Name, c.AccountId))
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Character lookup failed for name={Name}", name);
            return null;
        }
    }
}

public sealed record CharacterSearchResult(uint Id, string Name, uint AccountId);
public sealed record CharacterIdentity(uint Id, string Name, uint AccountId);
