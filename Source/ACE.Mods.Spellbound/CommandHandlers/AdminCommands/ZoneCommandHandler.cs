using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Helpers;
using ACE.Mods.Spellbound.Model;
using ACE.Mods.Spellbound.Services;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.CommandHandlers.AdminCommands
{
    public class ZoneCommandHandler : SpellboundPatchBase
    {
        public ZoneCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("zone", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld,
            "Inspect or mutate the Zone for the landblock you're standing in. " +
            "/zone shows the record + available stages, /zone name <name...> creates or renames, " +
            "/zone stage shows available stage SQL files, /zone stage <n> advances the stage, " +
            "/zone settele records your current position as the zone's tele point, " +
            "/zone tele [<name>] teleports you to the (current or named) zone's tele point.",
            "[name <name...> | stage [<n>] | settele | tele [<name>]]")]
        public static void HandleZone(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null) return;

            var landblock = player.CurrentLandblock;
            if (landblock == null)
            {
                player.Tell("You aren't in a loaded landblock.");
                return;
            }

            var key = LandblockNaming.Format(landblock.Id.Raw);

            if (parameters == null || parameters.Length == 0)
            {
                ShowZoneInfo(player, key);
                return;
            }

            var verb = parameters[0].ToLowerInvariant();
            var rest = parameters.Skip(1).ToArray();

            switch (verb)
            {
                case "name":
                    HandleName(player, key, rest);
                    return;
                case "stage":
                    HandleStage(player, key, rest);
                    return;
                case "settele":
                    HandleSetTele(player, key);
                    return;
                case "tele":
                    HandleTele(player, key, rest);
                    return;
                default:
                    player.Tell("Usage: /zone | /zone name <name...> | /zone stage [<n>] | /zone settele | /zone tele [<name>]");
                    return;
            }
        }

        private static void HandleName(Player player, string key, string[] rest)
        {
            var name = string.Join(' ', rest).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                player.Tell("Usage: /zone name <name...>");
                return;
            }

            var setBy = (uint?)player.Account?.AccountId;

            using var db = CreateDbContext();
            var existing = db.Zones.FirstOrDefault(z => z.Landblock == key);

            // Pre-check the name uniqueness invariant. A unique index on Name backs this
            // up at the DB level, but pre-checking lets us name the conflicting zone in
            // the error instead of just bouncing a DbUpdateException.
            var conflict = db.Zones.AsNoTracking()
                .FirstOrDefault(z => z.Name == name && (existing == null || z.Id != existing.Id));
            if (conflict != null)
            {
                player.Tell($"Name '{name}' is already taken by zone at {conflict.Landblock} (id {conflict.Id}).");
                return;
            }

            if (existing == null)
            {
                db.Zones.Add(new Zone
                {
                    Landblock = key,
                    Name = name,
                    SetByAccountId = setBy,
                    UpdatedAt = DateTime.UtcNow,
                });
                db.SaveChanges();
                player.Tell($"Created zone {key} -> '{name}'.");
                SpellboundLog.Info($"/zone name: {player.Name} created zone {key} -> '{name}'.");
                return;
            }

            var previous = existing.Name;
            existing.Name = name;
            existing.SetByAccountId = setBy;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Version++;
            db.SaveChanges();
            player.Tell($"Renamed zone {key}: '{previous}' -> '{name}'.");
            SpellboundLog.Info($"/zone name: {player.Name} renamed zone {key} from '{previous}' to '{name}'.");
            if (existing.Stage > 0)
                player.Tell($"  (note: this zone is at stage {existing.Stage}; rename Content/zone-stages/'{previous}' -> '{name}' if stage SQL directories exist.)");
        }

        private static void HandleStage(Player player, string key, string[] rest)
        {
            // Resolve the zone first — both the read (no number) and write (number) paths
            // need it, and a missing zone is a usage error either way.
            Zone? zone;
            using (var lookupDb = CreateDbContext())
            {
                zone = lookupDb.Zones.AsNoTracking().FirstOrDefault(z => z.Landblock == key);
            }
            if (zone == null)
            {
                player.Tell($"No zone for {key}. Use /zone name <name...> to create one first.");
                return;
            }

            // No stage number: show what's available on disk for this zone.
            if (rest.Length == 0 || string.IsNullOrWhiteSpace(rest[0]))
            {
                ShowAvailableStages(player, zone);
                return;
            }

            if (!int.TryParse(rest[0], out var stage))
            {
                player.Tell($"Stage value '{rest[0]}' isn't a number. Usage: /zone stage [<n>]");
                return;
            }

            if (stage < 0 || stage > 10)
            {
                player.Tell("Stage must be 0..10.");
                return;
            }

            player.Tell($"Setting zone '{zone.Name}' to stage {stage}.");
            SpellboundLog.Info($"/zone stage: {player.Name} setting zone '{zone.Name}' (id {zone.Id}) -> stage {stage}.");
            WorldStateService.SetZoneStage(zone.Id, stage);
        }

        private static void HandleSetTele(Player player, string key)
        {
            var pos = player.Location;
            if (pos == null)
            {
                player.Tell("Couldn't read your current position.");
                return;
            }

            using var db = CreateDbContext();
            var existing = db.Zones.FirstOrDefault(z => z.Landblock == key);
            if (existing == null)
            {
                player.Tell($"No zone for {key}. Use /zone name <name...> to create one first.");
                return;
            }

            existing.TeleCell = pos.Cell;
            existing.TelePosX = pos.PositionX;
            existing.TelePosY = pos.PositionY;
            existing.TelePosZ = pos.PositionZ;
            existing.TeleRotX = pos.RotationX;
            existing.TeleRotY = pos.RotationY;
            existing.TeleRotZ = pos.RotationZ;
            existing.TeleRotW = pos.RotationW;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Version++;
            db.SaveChanges();

            player.Tell($"Set tele point for zone '{existing.Name}' to your current position.");
            SpellboundLog.Info(
                $"/zone settele: {player.Name} set tele for zone '{existing.Name}' (id {existing.Id}) to cell 0x{pos.Cell:X8} ({pos.PositionX:F2}, {pos.PositionY:F2}, {pos.PositionZ:F2}).");
        }

        private static void HandleTele(Player player, string key, string[] rest)
        {
            // No name -> current landblock's zone. With a name -> look up by Name.
            // Name uniqueness isn't enforced at the schema level; FirstOrDefault
            // picks one if there happen to be dupes (admin-induced misconfig).
            var nameQuery = string.Join(' ', rest).Trim();

            Zone? zone;
            using (var db = CreateDbContext())
            {
                zone = string.IsNullOrWhiteSpace(nameQuery)
                    ? db.Zones.AsNoTracking().FirstOrDefault(z => z.Landblock == key)
                    : db.Zones.AsNoTracking().FirstOrDefault(z => z.Name == nameQuery);
            }

            if (zone == null)
            {
                player.Tell(string.IsNullOrWhiteSpace(nameQuery)
                    ? $"No zone for {key}."
                    : $"No zone named '{nameQuery}'.");
                return;
            }

            if (!zone.HasTele)
            {
                player.Tell($"Zone '{zone.Name}' has no tele point set. Use /zone settele while standing where you want it.");
                return;
            }

            var dest = new Position(
                zone.TeleCell!.Value,
                zone.TelePosX!.Value, zone.TelePosY!.Value, zone.TelePosZ!.Value,
                zone.TeleRotX!.Value, zone.TeleRotY!.Value, zone.TeleRotZ!.Value, zone.TeleRotW!.Value);
            WorldObject.AdjustDungeon(dest);

            SpellboundLog.Info(
                $"/zone tele: {player.Name} -> zone '{zone.Name}' (id {zone.Id}) at cell 0x{dest.Cell:X8}.");
            player.Teleport(dest);
        }

        private static void ShowZoneInfo(Player player, string key)
        {
            Zone? zone;
            using (var db = CreateDbContext())
            {
                zone = db.Zones.AsNoTracking().FirstOrDefault(z => z.Landblock == key);
            }

            if (zone == null)
            {
                player.Tell($"No zone for {key}. Use /zone name <name...> to create one.");
                return;
            }

            player.Tell($"=== Zone: {zone.Name} ({key}) ===");
            player.Tell($"Current stage: {zone.Stage}    Last updated: {zone.UpdatedAt:yyyy-MM-dd HH:mm}");
            ShowAvailableStages(player, zone);
        }

        private static void ShowAvailableStages(Player player, Zone zone)
        {
            // Mirrors the path resolution in WorldStateService.TryResolveStageFile so admins
            // see exactly what /zone stage <n> would actually find.
            var root = Settings?.ZoneStagesDirectory ?? string.Empty;
            if (string.IsNullOrEmpty(root))
            {
                player.Tell("(ZoneStagesDirectory is unconfigured; no stage files to enumerate.)");
                return;
            }

            var contentFolder = Path.Combine(root, zone.Name);
            var di = new DirectoryInfo(contentFolder);
            if (!di.Exists)
            {
                player.Tell($"(no stage directory at {contentFolder} — pure alias)");
                return;
            }

            var stages = di.GetFiles("*.sql")
                .Select(f => Path.GetFileNameWithoutExtension(f.Name))
                .Where(n => int.TryParse(n, out _))
                .OrderBy(n => int.Parse(n))
                .ToList();

            if (stages.Count == 0)
                player.Tell($"No stage SQL files in {contentFolder}.");
            else
                player.Tell($"Available stages: {string.Join(", ", stages)}");
        }
    }
}
