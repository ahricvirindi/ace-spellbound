using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model;
using ACE.Mods.Spellbound.Services;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.CommandHandlers.Admin
{
    public class SetTownStageCommandHandler : SpellboundPatchBase
    {
        public SetTownStageCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("settownstage", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, "Sets the current Town stage for the current landblock.")]
        public static void HandleSetTownStage(Session session, params string[] parameters)
        {
            SpellboundLog.Info("CommandHandlers.Admin.SetTownStageCommandHandler.HandleSetTownStage()");

            if (parameters == null || parameters.Length < 1 || string.IsNullOrWhiteSpace(parameters[0]))
            {
                SpellboundLog.Info("Usage: /settownstage <0-10>");
                return;
            }

            if (!int.TryParse(parameters[0], out int stage))
            {
                SpellboundLog.Info($"Town Stage provided as '{parameters[0]}' not valid.");
                return;
            }

            if (stage < 0 || stage > 10)
            {
                SpellboundLog.Info("Town Stage provided must be 0 to 10.");
                return;
            }

            var landblock = session.Player.CurrentLandblock;
            if (landblock == null)
            {
                SpellboundLog.Info("Player is not in a landblock.");
                return;
            }
            var landblockId = $"0x{(landblock.Id.Raw | 0xFFFF):X8}";

            // Resolve current player's landblock → Town record. The actual import,
            // DB update, and landblock reload all live in WorldStateService so the
            // event-driven path uses the exact same code.
            Town? town;
            using (var lookupDb = CreateDbContext())
            {
                town = lookupDb.Towns.AsNoTracking().FirstOrDefault(x => x.Landblock == landblockId);
            }

            if (town == null)
            {
                SpellboundLog.Info($"No Town definition found for landblock {landblockId}.");
                return;
            }

            SpellboundLog.Info($"Setting {town.Name} to stage {stage}.");
            WorldStateService.SetTownStage(town.Id, stage);
        }
    }
}
