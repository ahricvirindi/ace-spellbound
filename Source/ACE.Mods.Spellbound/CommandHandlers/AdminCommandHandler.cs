using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model;
using ACE.Server.Command.Handlers.Processors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Spellbound.CommandHandlers
{
    public class AdminCommandHandler : SpellboundPatchBase
    {
        public AdminCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) {

        }

        [CommandHandler("settownstage", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, "Sets the current Town stage for the current landblock.")]
        public static void HandleSetTownStage(Session session, params string[] parameters)
        {
            ModManager.Log("Spellbound.HandleSetTownStage()");

            int stage = 0;
            if (!int.TryParse(parameters[0] ?? "0", out stage)) {
                ModManager.Log($"Town Stage provided as {parameters[0] ?? "0"} not valid.");
                return;
            }

            if (stage < 0 || stage > 10)
            {
                ModManager.Log($"Town Stage provided must be 0 to 10.");
                return;
            }

            var landblock = session.Player.CurrentLandblock;
            var landblockId = $"0x{(landblock.Id.Raw | 0xFFFF):X8}";

            var db = CreateDbContext();
            Town? town = db.Towns.FirstOrDefault(x => x.Landblock == landblockId);

            if (town == null)
            {
                ModManager.Log($"No Town definition found for landblock {landblockId}.");
                return;
            }

            var content_folder = $"{Settings.TownStagesDirectory}\\{town.Name}";
            var di = new DirectoryInfo(content_folder);
            if (!di.Exists) {
                ModManager.Log($"No Town Staging directory found.  Expected: {content_folder}.");
                return;
            }

            var stage_file = di.GetFiles($"{stage}.sql").Select(x => x.FullName).FirstOrDefault();
            if (stage_file == null) {
                ModManager.Log($"No SQL file found for Town Staging.  Expected: {content_folder}\\{stage}.sql.");
                return;
            }

            ModManager.Log($"Setting {town.Name} to stage {stage} using {stage_file}.");
            try
            {
                DeveloperContentCommands.ImportSQL(stage_file);
            } catch (Exception ex)
            {
                ModManager.Log($"[ERROR] Could not set town stage.  Please check the logs and/or syntax of the SQL file at: {stage_file}.  Error was: {ex.Message}.");
                return;
            }

            // TODO : update town status to stage
            town.Stage = stage;
            town.UpdatedAt = DateTime.Now;
            db.SaveChanges();

            // destroy all non-player server objects
            landblock.DestroyAllNonPlayerObjects();

            // clear landblock cache
            DatabaseManager.World.ClearCachedInstancesByLandblock(landblock.Id.Landblock);

            // reload landblock
            var actionChain = new ActionChain();
            actionChain.AddDelayForOneTick();
            actionChain.AddAction(session.Player, () =>
            {
                landblock.Init(true);
            });
            actionChain.EnqueueChain();
        }

        [CommandHandler("who", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, "Displays who is online.")]
        public static void HandleWho(Session session, params string[] parameters)
        {
            ModManager.Log($"Spellbound.HandleWho()");
        }
    }
}
