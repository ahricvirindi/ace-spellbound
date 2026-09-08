using ACE.DatLoader;
using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Model;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Services
{
    // Snapshot writers for the web app's read paths. The mod owns these; the
    // web app is read-only against the rows produced here.
    //
    //   * OnlinePlayers           — /who roster. Insert on PlayerEnterWorld,
    //                               delete on FinalizeLogout, full rebuild
    //                               every 5 minutes from PlayerManager.
    //   * CharacterProfileSnapshots — profile page. Written on logout and
    //                               every 30 minutes for online characters.
    //
    // All write paths enter via these methods so the schema-shape decisions
    // (which fields, JSON skill encoding) live in one place.
    public static class SnapshotService
    {
        // ---------- Roster (OnlinePlayers) ----------

        public static OnlinePlayer BuildOnlineRow(Player p)
        {
            var landblockRaw = p.Location?.LandblockId.Raw;
            return new OnlinePlayer
            {
                CharacterId = p.Guid.Full,
                AccountId = (int)p.Account.AccountId,
                CharacterName = p.Name ?? string.Empty,
                Level = (int)(p.Level ?? 0),
                Landblock = landblockRaw,
                LandblockName = null,
                SeenAt = DateTime.UtcNow
            };
        }

        public static void WriteOnline(SpellboundContext db, OnlinePlayer row)
        {
            // Resolve landblock name lazily so the patch thread doesn't pay for
            // a Zones lookup; we already have a db context here.
            if (row.Landblock.HasValue && string.IsNullOrEmpty(row.LandblockName))
                row.LandblockName = LandblockNaming.Resolve(row.Landblock.Value, db);

            var existing = db.OnlinePlayers.FirstOrDefault(x => x.CharacterId == row.CharacterId);
            if (existing == null)
            {
                db.OnlinePlayers.Add(row);
            }
            else
            {
                existing.AccountId = row.AccountId;
                existing.CharacterName = row.CharacterName;
                existing.Level = row.Level;
                existing.Landblock = row.Landblock;
                existing.LandblockName = row.LandblockName;
                existing.SeenAt = row.SeenAt;
            }
            db.SaveChanges();
        }

        public static void RemoveOnline(SpellboundContext db, uint characterId)
        {
            // Raw SQL DELETE rather than db.OnlinePlayers.Where(...).ExecuteDelete()
            // because the mod compiles against EF Core 8.0 but runs in the ACE
            // server process which loads EF Core 9.x — RelationalQueryableExtensions.
            // ExecuteDelete was promoted/relocated between versions and throws
            // MissingMethodException at runtime under that mix. ExecuteSqlInterpolated
            // lives on RelationalDatabaseFacadeExtensions which has been stable
            // since EF Core 3.0+, so it works regardless of which version we end
            // up bound against.
            db.Database.ExecuteSqlInterpolated(
                $"DELETE FROM `OnlinePlayers` WHERE `CharacterId` = {characterId}");
        }

        // Full-rebuild path used by the 5-minute timer. Truth source is the
        // passed-in player list; the table is wiped and rewritten in a single
        // transaction so /who never observes an empty roster mid-rebuild.
        public static void RebuildRoster(SpellboundContext db, IReadOnlyList<Player> online)
        {
            var rows = online
                .Where(p => p?.Account != null)
                .Select(BuildOnlineRow)
                .ToList();

            // Resolve all landblock names in one round-trip.
            var nameByRaw = LandblockNaming.ResolveBatch(
                rows.Where(r => r.Landblock.HasValue).Select(r => r.Landblock!.Value),
                db);
            foreach (var r in rows)
                if (r.Landblock.HasValue && nameByRaw.TryGetValue(r.Landblock.Value, out var nm))
                    r.LandblockName = nm;

            using var tx = db.Database.BeginTransaction();
            // ExecuteSqlRaw not ExecuteDelete — see RemoveOnline for the EF
            // 8/9 cross-binding rationale.
            db.Database.ExecuteSqlRaw("DELETE FROM `OnlinePlayers`");
            db.OnlinePlayers.AddRange(rows);
            db.SaveChanges();
            tx.Commit();
        }

        // ---------- Profile (CharacterProfileSnapshots) ----------

        public static CharacterProfileSnapshot BuildProfileRow(Player p)
        {
            return new CharacterProfileSnapshot
            {
                CharacterId = p.Guid.Full,
                AccountId = (int)p.Account.AccountId,
                CharacterName = p.Name ?? string.Empty,
                Level = (int)(p.Level ?? 0),
                TotalXp = p.TotalExperience ?? 0,
                UnassignedXp = p.AvailableExperience ?? 0,

                Strength = (int)p.Strength.Base,
                Endurance = (int)p.Endurance.Base,
                Coordination = (int)p.Coordination.Base,
                Quickness = (int)p.Quickness.Base,
                Focus = (int)p.Focus.Base,
                Self = (int)p.Self.Base,

                HealthMax = (int)p.Health.MaxValue,
                StaminaMax = (int)p.Stamina.MaxValue,
                ManaMax = (int)p.Mana.MaxValue,

                SkillsJson = SerializeSkills(p),
                SnapshottedAt = DateTime.UtcNow
            };
        }

        public static void WriteProfile(SpellboundContext db, CharacterProfileSnapshot row)
        {
            var existing = db.CharacterProfileSnapshots
                .FirstOrDefault(x => x.CharacterId == row.CharacterId);
            if (existing == null)
            {
                db.CharacterProfileSnapshots.Add(row);
            }
            else
            {
                existing.AccountId = row.AccountId;
                existing.CharacterName = row.CharacterName;
                existing.Level = row.Level;
                existing.TotalXp = row.TotalXp;
                existing.UnassignedXp = row.UnassignedXp;
                existing.Strength = row.Strength;
                existing.Endurance = row.Endurance;
                existing.Coordination = row.Coordination;
                existing.Quickness = row.Quickness;
                existing.Focus = row.Focus;
                existing.Self = row.Self;
                existing.HealthMax = row.HealthMax;
                existing.StaminaMax = row.StaminaMax;
                existing.ManaMax = row.ManaMax;
                existing.SkillsJson = row.SkillsJson;
                existing.SnapshottedAt = row.SnapshottedAt;
            }
            db.SaveChanges();
        }

        // ---------- Skill JSON shape ----------

        // The web app deserializes this into its own DTO; keep the shape stable
        // unless you also bump the consumer. Skipping skills with state ==
        // Untrained because there are ~30 of them and the profile page only
        // cares about ones the player has actually invested in. The web app
        // can show "Untrained" for skills not present in the array.
        private static string SerializeSkills(Player p)
        {
            var entries = new List<SkillSnapshotEntry>(p.Skills.Count);
            foreach (var (skill, cs) in p.Skills)
            {
                if (cs.AdvancementClass == SkillAdvancementClass.Inactive ||
                    cs.AdvancementClass == SkillAdvancementClass.Untrained)
                    continue;

                entries.Add(new SkillSnapshotEntry
                {
                    Skill = skill.ToString(),
                    State = cs.AdvancementClass.ToString(),
                    Base = (int)cs.Base,
                    Current = (int)cs.Current
                });
            }
            return JsonSerializer.Serialize(entries);
        }

        // Public so the web app can deserialize against the same shape.
        public sealed class SkillSnapshotEntry
        {
            public string Skill { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public int Base { get; set; }
            public int Current { get; set; }
        }

        // ---------- Equipment (CharacterEquipmentSnapshots) ----------

        public static CharacterEquipmentSnapshot BuildEquipmentRow(Player p)
        {
            return new CharacterEquipmentSnapshot
            {
                CharacterId = p.Guid.Full,
                AccountId = (int)p.Account.AccountId,
                CharacterName = p.Name ?? string.Empty,
                EquipmentJson = SerializeEquipment(p),
                SnapshottedAt = DateTime.UtcNow
            };
        }

        public static void WriteEquipment(SpellboundContext db, CharacterEquipmentSnapshot row)
        {
            var existing = db.CharacterEquipmentSnapshots
                .FirstOrDefault(x => x.CharacterId == row.CharacterId);
            if (existing == null)
            {
                db.CharacterEquipmentSnapshots.Add(row);
            }
            else
            {
                existing.AccountId = row.AccountId;
                existing.CharacterName = row.CharacterName;
                existing.EquipmentJson = row.EquipmentJson;
                existing.SnapshottedAt = row.SnapshottedAt;
            }
            db.SaveChanges();
        }

        // Public so the web app can deserialize against the same shape.
        // Requirements is currently writer-empty — the mod-side capture in
        // SerializeEquipment doesn't translate WieldDifficulty / WieldSkillType
        // / WieldRequirements yet. Test data hand-curates the field for visual
        // testing; real-data capture is a follow-up under the equipment-detail
        // work in TODO.md.
        public sealed class EquippedItemEntry
        {
            public string Name { get; set; } = string.Empty;
            public string Slot { get; set; } = string.Empty;
            public uint IconId { get; set; }
            public int? Workmanship { get; set; }
            public int? Damage { get; set; }
            public int? ArmorLevel { get; set; }
            public int? Spellcraft { get; set; }
            public int? MaxMana { get; set; }
            public int? Value { get; set; }
            public List<string> Spells { get; set; } = new();
            public List<string> Requirements { get; set; } = new();
        }

        // Walks Player.EquippedObjects and emits one EquippedItemEntry per
        // wielded item. Unknown spell ids fall back to "spell:<id>" rather
        // than dropping silently — easier to spot drift between the dat
        // SpellTable and persisted spell ids.
        private static string SerializeEquipment(Player p)
        {
            var entries = new List<EquippedItemEntry>(p.EquippedObjects.Count);
            foreach (var item in p.EquippedObjects.Values)
            {
                if (item == null) continue;

                var spells = new List<string>();
                var spellBook = item.Biota?.PropertiesSpellBook;
                if (spellBook != null)
                {
                    foreach (var spellId in spellBook.Keys)
                    {
                        if (DatManager.PortalDat?.SpellTable?.Spells != null
                            && DatManager.PortalDat.SpellTable.Spells.TryGetValue((uint)spellId, out var spell))
                            spells.Add(spell.Name);
                        else
                            spells.Add($"spell:{spellId}");
                    }
                }

                entries.Add(new EquippedItemEntry
                {
                    Name = item.Name ?? string.Empty,
                    Slot = item.CurrentWieldedLocation?.ToString() ?? "Unknown",
                    IconId = item.IconId,
                    Workmanship = item.GetProperty(PropertyInt.ItemWorkmanship),
                    Damage = item.GetProperty(PropertyInt.Damage),
                    ArmorLevel = item.GetProperty(PropertyInt.ArmorLevel),
                    Spellcraft = item.GetProperty(PropertyInt.ItemSpellcraft),
                    MaxMana = item.GetProperty(PropertyInt.ItemMaxMana),
                    Value = item.GetProperty(PropertyInt.Value),
                    Spells = spells
                });
            }
            return JsonSerializer.Serialize(entries);
        }
    }
}
