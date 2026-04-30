using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model;

namespace ACE.Mods.Spellbound.CommandHandlers.AdminCommands
{
    public class ReserveNameCommandHandler : SpellboundPatchBase
    {
        public ReserveNameCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("reservename", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Permanently reserve a character name to an account. Used to retroactively claim names missed by season-wipe.sql.",
            "<characterName> <accountName>")]
        public static void HandleReserveName(Session session, params string[] parameters)
        {
            var caller = session?.Player;
            if (parameters == null || parameters.Length < 2 ||
                string.IsNullOrWhiteSpace(parameters[0]) || string.IsNullOrWhiteSpace(parameters[1]))
            {
                caller?.Tell("Usage: /reservename <characterName> <accountName>");
                return;
            }

            var characterName = parameters[0].Trim();
            var accountName = parameters[1].Trim();

            var accountId = DatabaseManager.Authentication.GetAccountIdByName(accountName);
            if (accountId == 0)
            {
                caller?.Tell($"No account found with name '{accountName}'.");
                return;
            }

            using var db = CreateDbContext();
            var existing = db.ReservedNames.FirstOrDefault(r => r.Name == characterName);
            if (existing != null)
            {
                if (existing.AccountId == accountId)
                {
                    caller?.Tell($"'{characterName}' is already reserved to account '{accountName}' ({accountId}).");
                    return;
                }

                caller?.Tell(
                    $"'{characterName}' is already reserved to a different account ({existing.AccountId}). Use /unreservename first to clear it.");
                return;
            }

            db.ReservedNames.Add(new ReservedName
            {
                Name = characterName,
                AccountId = accountId,
                ReservedAt = DateTime.UtcNow,
            });
            db.SaveChanges();

            var actor = caller?.Name ?? "(console)";
            caller?.Tell($"Reserved '{characterName}' for account '{accountName}' ({accountId}).");
            SpellboundLog.Info(
                $"/reservename: {actor} reserved '{characterName}' to account '{accountName}' ({accountId}).");
        }
    }
}
