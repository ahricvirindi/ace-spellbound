using ACE.Mods.Spellbound.Data;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Helpers
{
    /// <summary>
    /// Centralizes landblock-id formatting and the resolve-to-human-name lookup.
    ///
    /// Format: matches the convention SetTownStageCommandHandler / Town.Landblock
    /// already use — full position id with the low 16 bits set, hex-formatted
    /// ("0xAABBFFFF"). Anything that talks to or queries the Town / LandblockAlias
    /// rows must use Format so the keys line up.
    ///
    /// Resolution priority: LandblockAlias.Name -> Town.Name -> formatted hex.
    /// </summary>
    public static class LandblockNaming
    {
        public static string Format(uint raw) => $"0x{(raw | 0xFFFF):X8}";

        public static string Resolve(uint raw, SpellboundContext db)
        {
            var key = Format(raw);

            var aliasName = db.LandblockAliases.AsNoTracking()
                .Where(a => a.Landblock == key)
                .Select(a => a.Name)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(aliasName))
                return aliasName;

            var townName = db.Towns.AsNoTracking()
                .Where(t => t.Landblock == key)
                .Select(t => t.Name)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(townName))
                return townName;

            return key;
        }

        // Batch form for /who: one round trip per source table instead of N.
        public static Dictionary<uint, string> ResolveBatch(IEnumerable<uint> raws, SpellboundContext db)
        {
            var distinctRaws = raws.Distinct().ToList();
            var result = new Dictionary<uint, string>(distinctRaws.Count);
            if (distinctRaws.Count == 0) return result;

            var keyByRaw = distinctRaws.ToDictionary(r => r, r => Format(r));
            var keys = keyByRaw.Values.Distinct().ToList();

            var aliases = db.LandblockAliases.AsNoTracking()
                .Where(a => keys.Contains(a.Landblock))
                .Select(a => new { a.Landblock, a.Name })
                .ToDictionary(x => x.Landblock, x => x.Name);

            var townKeys = keys.Where(k => !aliases.ContainsKey(k)).ToList();
            var towns = townKeys.Count == 0
                ? new Dictionary<string, string>()
                : db.Towns.AsNoTracking()
                    .Where(t => townKeys.Contains(t.Landblock))
                    .Select(t => new { t.Landblock, t.Name })
                    .ToDictionary(x => x.Landblock, x => x.Name);

            foreach (var (raw, key) in keyByRaw)
            {
                if (aliases.TryGetValue(key, out var aliasName) && !string.IsNullOrWhiteSpace(aliasName))
                    result[raw] = aliasName;
                else if (towns.TryGetValue(key, out var townName) && !string.IsNullOrWhiteSpace(townName))
                    result[raw] = townName;
                else
                    result[raw] = key;
            }

            return result;
        }
    }
}
