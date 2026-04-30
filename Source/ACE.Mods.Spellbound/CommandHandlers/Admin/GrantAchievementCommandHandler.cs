using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Services;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.CommandHandlers.Admin
{
    public class GrantAchievementCommandHandler : SpellboundPatchBase
    {
        public GrantAchievementCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("grantachievement", AccessLevel.Admin, CommandHandlerFlag.None, 2,
            "Force-grant an achievement to an account, bypassing AmountRequired. Re-walks characters if already granted.",
            "<accountName> <achievementId>")]
        public static void HandleGrantAchievement(Session session, params string[] parameters)
        {
            if (parameters == null || parameters.Length < 2 ||
                string.IsNullOrWhiteSpace(parameters[0]) || string.IsNullOrWhiteSpace(parameters[1]))
            {
                session?.Player.Tell("Usage: /grantachievement <accountName> <achievementId>");
                return;
            }

            var accountName = parameters[0];
            if (!int.TryParse(parameters[1], out var achievementId))
            {
                session?.Player.Tell($"Invalid achievement id '{parameters[1]}'.");
                return;
            }

            var accountId = DatabaseManager.Authentication.GetAccountIdByName(accountName);
            if (accountId == 0)
            {
                session?.Player.Tell($"No account found with name '{accountName}'.");
                return;
            }

            using var db = CreateDbContext();
            var ach = db.Achievement.AsNoTracking().FirstOrDefault(a => a.Id == achievementId);
            if (ach == null)
            {
                session?.Player.Tell($"No achievement found with id {achievementId}.");
                return;
            }

            var amountRequired = Math.Max(1, ach.AmountRequired ?? 1);

            int rows;
            try
            {
                // Reason for inline SQL: INSERT...ON
                // DUPLICATE KEY UPDATE is one atomic upsert with row-level X-locking. The
                // COALESCE(AwardedAt, UTC_TIMESTAMP()) preserves an earlier award timestamp if the
                // row already exists; GREATEST(Progress, required) makes the force-grant idempotent.
                // EF's read → mutate → SaveChanges would need a separate SELECT and would race against
                // any concurrent legit award fire mid-command.
                rows = db.Database.ExecuteSqlInterpolated($@"
                    INSERT INTO `AccountAchievements`
                        (`AccountId`, `AchievementId`, `Progress`, `AwardedAt`, `Version`)
                    VALUES
                        ({(int)accountId}, {ach.Id}, {amountRequired}, UTC_TIMESTAMP(6), 1)
                    ON DUPLICATE KEY UPDATE
                        `AwardedAt` = COALESCE(`AwardedAt`, UTC_TIMESTAMP(6)),
                        `Progress`  = GREATEST(`Progress`, {amountRequired}),
                        `Version`   = `Version` + 1;");
            }
            catch (Exception ex)
            {
                SpellboundLog.Error($"/grantachievement: SQL failed for account {accountId}, ach {ach.Id}: {ex}");
                session?.Player.Tell("Grant failed; see server log.");
                return;
            }

            var actor = session?.Player?.Name ?? "(console)";
            session?.Player.Tell($"Granted '{ach.Name}' (id {ach.Id}) to '{accountName}' (account {accountId}). Walking characters.");
            SpellboundLog.Info($"/grantachievement: '{ach.Name}' force-granted to account {accountName} ({accountId}) by {actor} (rows affected: {rows}).");

            AchievementService.ApplyToAllAccountCharacters(accountId, new[] { ach });
        }
    }
}
