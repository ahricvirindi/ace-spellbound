using ACE.Database.Models.Auth;
using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.CommandHandlers.AdminCommands
{
    public class ListReservedNamesCommandHandler : SpellboundPatchBase
    {
        public ListReservedNamesCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("reservednames", AccessLevel.Admin, CommandHandlerFlag.None,
            "List reserved character names. With no argument, shows all reservations grouped by account name. With an accountName, shows just that account's reservations.",
            "[accountName]")]
        public static void HandleListReservedNames(Session session, params string[] parameters)
        {
            var caller = session?.Player;
            if (caller == null) return;

            using var db = CreateDbContext();
            IQueryable<ReservedName> query = db.ReservedNames.AsNoTracking();

            string? filterAccountName = null;
            if (parameters != null && parameters.Length >= 1 && !string.IsNullOrWhiteSpace(parameters[0]))
            {
                filterAccountName = parameters[0].Trim();
                var accountId = DatabaseManager.Authentication.GetAccountIdByName(filterAccountName);
                if (accountId == 0)
                {
                    caller.Tell($"No account found with name '{filterAccountName}'.");
                    return;
                }

                query = query.Where(r => r.AccountId == accountId);
            }

            var rows = query.ToList();
            if (rows.Count == 0)
            {
                caller.Tell(filterAccountName == null
                    ? "No reserved names."
                    : $"No reserved names for account '{filterAccountName}'.");
                return;
            }

            // Resolve account names in one cross-database hop. AuthDbContext is a separate
            // EF context against ace_auth — can't be JOINed in the Spellbound query, so we
            // batch-resolve distinct ids here. Unknown ids (account purged) render as "?".
            var distinctIds = rows.Select(r => r.AccountId).Distinct().ToArray();
            Dictionary<uint, string> nameById;
            using (var auth = new AuthDbContext())
            {
                nameById = auth.Account
                    .AsNoTracking()
                    .Where(a => distinctIds.Contains(a.AccountId))
                    .ToDictionary(a => a.AccountId, a => a.AccountName);
            }

            var ordered = rows
                .OrderBy(r => nameById.TryGetValue(r.AccountId, out var n) ? n : "~", StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var header = filterAccountName == null
                ? $"=== {ordered.Count} reserved names ==="
                : $"=== {ordered.Count} reserved for '{filterAccountName}' ===";
            caller.Tell(header);

            foreach (var r in ordered)
            {
                var accountName = nameById.TryGetValue(r.AccountId, out var n) ? n : "?";
                caller.Tell($"  [{accountName}] {r.Name} (account {r.AccountId}, since {r.ReservedAt:yyyy-MM-dd})");
            }
        }
    }
}
