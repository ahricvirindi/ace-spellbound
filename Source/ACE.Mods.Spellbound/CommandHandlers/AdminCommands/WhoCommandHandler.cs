using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Helpers;

namespace ACE.Mods.Spellbound.CommandHandlers.AdminCommands
{
    public class WhoCommandHandler : SpellboundPatchBase
    {
        public WhoCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("who", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld,
            "List online players with account name, level, and current location.")]
        public static void HandleWho(Session session, params string[] parameters)
        {
            var caller = session?.Player;
            if (caller == null) return;

            var online = PlayerManager.GetAllOnline();
            if (online == null || online.Count == 0)
            {
                caller.Tell("No players online.");
                return;
            }

            var rawByPlayer = online.ToDictionary(
                p => p,
                p => p.Location?.LandblockId.Raw ?? 0u);

            using var db = CreateDbContext();
            var nameByRaw = LandblockNaming.ResolveBatch(rawByPlayer.Values, db);

            caller.Tell($"=== {online.Count} online ===");
            foreach (var p in online.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                var level = p.Level ?? 0;
                var account = p.Account?.AccountName ?? "?";
                var location = nameByRaw.TryGetValue(rawByPlayer[p], out var loc) ? loc : "?";
                caller.Tell($"  {p.Name} (lv {level}) — {location} [{account}]");
            }
        }
    }
}
