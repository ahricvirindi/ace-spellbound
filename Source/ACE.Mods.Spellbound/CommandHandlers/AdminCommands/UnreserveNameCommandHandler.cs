using ACE.Mods.Spellbound.Base;

namespace ACE.Mods.Spellbound.CommandHandlers.AdminCommands
{
    public class UnreserveNameCommandHandler : SpellboundPatchBase
    {
        public UnreserveNameCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("unreservename", AccessLevel.Admin, CommandHandlerFlag.None, 1,
            "Remove a permanent character-name reservation, freeing the name for any account to claim.",
            "<characterName>")]
        public static void HandleUnreserveName(Session session, params string[] parameters)
        {
            var caller = session?.Player;
            if (parameters == null || parameters.Length < 1 || string.IsNullOrWhiteSpace(parameters[0]))
            {
                caller?.Tell("Usage: /unreservename <characterName>");
                return;
            }

            var characterName = parameters[0].Trim();

            using var db = CreateDbContext();
            var existing = db.ReservedNames.FirstOrDefault(r => r.Name == characterName);
            if (existing == null)
            {
                caller?.Tell($"'{characterName}' is not reserved.");
                return;
            }

            var priorAccountId = existing.AccountId;
            db.ReservedNames.Remove(existing);
            db.SaveChanges();

            var actor = caller?.Name ?? "(console)";
            caller?.Tell($"Cleared reservation on '{characterName}' (was account {priorAccountId}).");
            SpellboundLog.Info(
                $"/unreservename: {actor} cleared reservation on '{characterName}' (was account {priorAccountId}).");
        }
    }
}
