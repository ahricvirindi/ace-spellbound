using ACE.Mods.Spellbound.Data;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Helpers
{
    /// <summary>
    /// Centralizes landblock-id formatting and the resolve-to-human-name lookup.
    ///
    /// Format: matches the convention ZoneCommandHandler / Zone.Landblock
    /// already use — full position id with the low 16 bits set, hex-formatted
    /// ("0xAABBFFFF"). Anything that talks to or queries Zone rows must use Format
    /// so the keys line up.
    ///
    /// Resolution: Zone.Name -> formatted hex.
    /// </summary>
    public static class LandblockNaming
    {
        public static string Format(uint raw) => $"0x{(raw | 0xFFFF):X8}";

        public static string Resolve(uint raw, SpellboundContext db)
        {
            var key = Format(raw);

            var name = db.Zones.AsNoTracking()
                .Where(z => z.Landblock == key)
                .Select(z => z.Name)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(name) ? key : name;
        }

        // Batch form for /who: one round trip instead of N.
        public static Dictionary<uint, string> ResolveBatch(IEnumerable<uint> raws, SpellboundContext db)
        {
            var distinctRaws = raws.Distinct().ToList();
            var result = new Dictionary<uint, string>(distinctRaws.Count);
            if (distinctRaws.Count == 0) return result;

            var keyByRaw = distinctRaws.ToDictionary(r => r, r => Format(r));
            var keys = keyByRaw.Values.Distinct().ToList();

            var nameByKey = db.Zones.AsNoTracking()
                .Where(z => keys.Contains(z.Landblock))
                .Select(z => new { z.Landblock, z.Name })
                .ToDictionary(x => x.Landblock, x => x.Name);

            foreach (var (raw, key) in keyByRaw)
            {
                if (nameByKey.TryGetValue(key, out var name) && !string.IsNullOrWhiteSpace(name))
                    result[raw] = name;
                else
                    result[raw] = key;
            }

            return result;
        }
    }
}
