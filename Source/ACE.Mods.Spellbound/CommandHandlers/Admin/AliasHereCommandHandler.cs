using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Helpers;
using ACE.Mods.Spellbound.Model;

namespace ACE.Mods.Spellbound.CommandHandlers.Admin
{
    public class AliasHereCommandHandler : SpellboundPatchBase
    {
        public AliasHereCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("aliashere", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1,
            "Set a human-readable name for the landblock you're currently standing in. Used by /who.",
            "<name...>")]
        public static void HandleAliasHere(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player == null) return;

            if (parameters == null || parameters.Length == 0)
            {
                player.Tell("Usage: /aliashere <name...>");
                return;
            }

            var landblock = player.CurrentLandblock;
            if (landblock == null)
            {
                player.Tell("You aren't in a loaded landblock.");
                return;
            }

            var name = string.Join(' ', parameters).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                player.Tell("Usage: /aliashere <name...>");
                return;
            }

            var key = LandblockNaming.Format(landblock.Id.Raw);
            var setBy = (uint?)player.Account?.AccountId;

            using var db = CreateDbContext();
            var existing = db.LandblockAliases.FirstOrDefault(a => a.Landblock == key);
            if (existing == null)
            {
                db.LandblockAliases.Add(new LandblockAlias
                {
                    Landblock = key,
                    Name = name,
                    SetByAccountId = setBy,
                    UpdatedAt = DateTime.UtcNow,
                });
                db.SaveChanges();
                player.Tell($"Aliased {key} -> '{name}'.");
                SpellboundLog.Info($"/aliashere: {player.Name} set {key} -> '{name}'.");
                return;
            }

            var previous = existing.Name;
            existing.Name = name;
            existing.SetByAccountId = setBy;
            existing.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
            player.Tell($"Renamed {key}: '{previous}' -> '{name}'.");
            SpellboundLog.Info($"/aliashere: {player.Name} renamed {key} from '{previous}' to '{name}'.");
        }
    }
}
