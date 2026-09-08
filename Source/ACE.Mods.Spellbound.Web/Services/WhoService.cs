using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Model;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Web.Services;

// /who roster reads — pure projection over the OnlinePlayers snapshot table.
// The mod's SnapshotTimers / login-logout patches own the writes; we just read.
// Snapshot is rebuilt every 5 minutes, so the data here can be up to ~5 minutes
// stale at the high end.
public sealed class WhoService(
    SpellboundContext db,
    ILogger<WhoService> logger)
{
    public async Task<IReadOnlyList<OnlinePlayer>> GetOnlineAsync()
    {
        try
        {
            return await db.OnlinePlayers
                .AsNoTracking()
                .OrderBy(p => p.CharacterName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load OnlinePlayers");
            return Array.Empty<OnlinePlayer>();
        }
    }
}
