using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.CommandHandlers.Admin
{
    public class TownInfoCommandHandler : SpellboundPatchBase
    {
        public TownInfoCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("towninfo", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld,
            "Show the Spellbound Town record + staging file presence for the landblock you're standing in.")]
        public static void HandleTownInfo(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null) return;

            var landblock = player.CurrentLandblock;
            if (landblock == null)
            {
                player.Tell("You are not in a landblock.");
                return;
            }

            var landblockId = $"0x{(landblock.Id.Raw | 0xFFFF):X8}";

            Town? town;
            using (var db = CreateDbContext())
            {
                town = db.Towns.AsNoTracking().FirstOrDefault(x => x.Landblock == landblockId);
            }

            if (town == null)
            {
                player.Tell($"No Town record for landblock {landblockId}.");
                return;
            }

            player.Tell($"=== Town: {town.Name} ({landblockId}) ===");
            player.Tell($"Current stage: {town.Stage}    Last updated: {town.UpdatedAt:yyyy-MM-dd HH:mm}");

            // Surface available stage SQL files so admins know what /settownstage <n> would
            // actually find. Mirrors the path resolution in WorldStateService.TryResolveStageFile.
            var root = Settings?.TownStagesDirectory ?? string.Empty;
            if (string.IsNullOrEmpty(root))
            {
                player.Tell("(TownStagesDirectory is unconfigured; no stage files to enumerate.)");
                return;
            }

            var contentFolder = Path.Combine(root, town.Name);
            var di = new DirectoryInfo(contentFolder);
            if (!di.Exists)
            {
                player.Tell($"Staging directory missing: {contentFolder}");
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
