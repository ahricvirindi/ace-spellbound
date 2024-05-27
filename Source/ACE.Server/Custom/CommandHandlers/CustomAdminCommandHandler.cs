using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Server.Command.Handlers.Processors;
using ACE.Server.Custom.Config;
using ACE.Server.Custom.Data;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using log4net.Core;
using System;
using System.IO;
using System.Linq;

namespace ACE.Server.Custom.CommandHandlers
{
    internal static class CustomAdminCommandHandler
    {
        // /settownstage [0-10]
        public static void HandleSetTownStage(Session session, string stageParamter = "0")
        {
            CustomHelper.Debug(session, "CustomAdminCommandHandler.HandleSetTownStage()");

            int stage = 0;
            if (!int.TryParse(stageParamter, out stage)) {
                CustomHelper.Debug(session, $"Town Stage provided as {stageParamter} not valid.");
                return;
            }

            if (stage < 0 || stage > 10)
            {
                CustomHelper.Debug(session, $"Town Stage provided must be 0 to 10.");
                return;
            }

            var landblock = session.Player.CurrentLandblock;
            var landblockId = $"0x{(landblock.Id.Raw | 0xFFFF):X8}";

            var db = new CustomContext();
            var town = db.Towns.FirstOrDefault(x => x.Landblock == landblockId);

            if (town == null)
            {
                CustomHelper.Debug(session, $"No Town definition found for landblock {landblockId}.");
                return;
            }

            var content_folder = $"{CustomConfigManager.Config.Custom.TownStagesDirectory}\\{town.Name}";
            var di = new DirectoryInfo(content_folder);
            if (!di.Exists) {
                CustomHelper.Debug(session, $"No Town Staging directory found.  Expected: {content_folder}.");
                return;
            }

            var stage_file = di.GetFiles($"{stage}.sql").Select(x => x.FullName).FirstOrDefault();
            if (stage_file == null) {
                CustomHelper.Debug(session, $"No SQL file found for Town Staging.  Expected: {content_folder}\\{stage}.sql.");
                return;
            }

            CustomHelper.Debug(session, $"Setting {town.Name} to stage {stage} using {stage_file}.");
            try
            {
                DeveloperContentCommands.ImportSQL(stage_file);
            } catch (Exception ex)
            {
                CustomHelper.Debug(session, $"[ERROR] Could not set town stage.  Please check the logs and/or syntax of the SQL file at: {stage_file}.  Error was: {ex.Message}.");
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

        public static void HandleWho(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomAdminCommandHandler.HandleWho()");
        }
    }
}
