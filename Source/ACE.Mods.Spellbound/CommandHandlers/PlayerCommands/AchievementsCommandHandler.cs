using ACE.Mods.Spellbound.Base;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.CommandHandlers.PlayerCommands
{
    public class AchievementsCommandHandler : SpellboundPatchBase
    {
        public AchievementsCommandHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [CommandHandler("achievements", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
            "Lists your account's earned and in-progress achievements.")]
        public static void HandleAchievements(Session session, params string[] parameters)
        {
            var player = session?.Player;
            if (player?.Account == null) return;

            var accountId = (uint)player.Account.AccountId;
            var accountName = player.Account.AccountName ?? $"account {accountId}";

            RunDbWork(db =>
            {
                var rows = db.AccountAchievements
                    .AsNoTracking()
                    .Include(aa => aa.Achievement)
                    .Where(aa => aa.AccountId == (int)accountId)
                    .ToList();

                if (rows.Count == 0)
                {
                    player.Tell("You have no achievements yet.");
                    return;
                }

                var earned = rows.Where(r => r.AwardedAt != null && r.Achievement != null).ToList();
                var inProgress = rows.Where(r => r.AwardedAt == null && r.Achievement != null).ToList();

                player.Tell($"=== Achievements for {accountName} ===");
                player.Tell($"Earned: {earned.Count}    In progress: {inProgress.Count}");

                if (earned.Count > 0)
                {
                    player.Tell("--- Earned ---");
                    foreach (var aa in earned.OrderBy(r => r.Achievement!.Name))
                    {
                        var ach = aa.Achievement!;
                        var awarded = aa.AwardedAt?.ToString("yyyy-MM-dd") ?? "?";
                        player.Tell($"  {ach.Name} — {ach.AwardType} +{ach.AwardValue ?? 0} (granted {awarded})");
                    }
                }

                if (inProgress.Count > 0)
                {
                    player.Tell("--- In progress ---");
                    foreach (var aa in inProgress.OrderBy(r => r.Achievement!.Name))
                    {
                        var ach = aa.Achievement!;
                        var required = Math.Max(1, ach.AmountRequired ?? 1);
                        player.Tell($"  {ach.Name} — {aa.Progress}/{required}");
                    }
                }
            });
        }
    }
}
