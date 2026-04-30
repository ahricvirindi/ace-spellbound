using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Services;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.EventHandlers.AchievementRules
{
    [HarmonyPatch]
    public sealed class PlayerOnCharacterCreatedHandler : SpellboundPatchBase
    {
        public PlayerOnCharacterCreatedHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.AddOfflinePlayer), new[] { typeof(Player) })]
        public static void OnCharacterCreated(Player player)
        {
            if (player?.Account == null) return;

            var accountId = (uint)player.Account.AccountId;
            var characterGuid = player.Guid.Full;
            var characterName = player.Name;

            RunDbWork(db =>
            {
                var awarded = db.AccountAchievements
                    .AsNoTracking()
                    .Where(aa => aa.AccountId == (int)accountId && aa.AwardedAt != null)
                    .Include(aa => aa.Achievement)
                    .Select(aa => aa.Achievement!)
                    .Where(a => a != null)
                    .ToList();

                if (awarded.Count == 0)
                    return;

                // Re-resolve the OfflinePlayer here so we observe whatever PlayerManager
                // has registered post-AddOfflinePlayer. Same biota reference as the live
                // Player passed into the postfix, but the offline path of ApplyToCharacter
                // skips the (premature) ActionChain + network-send branch.
                var offline = PlayerManager.GetOfflinePlayer(characterGuid);
                if (offline == null)
                {
                    SpellboundLog.Warn(
                        $"OnCharacterCreated: OfflinePlayer for {characterName} ({characterGuid:X8}) not registered post-AddOfflinePlayer; skipping {awarded.Count} achievement(s).");
                    return;
                }

                SpellboundLog.Info(
                    $"OnCharacterCreated: applying {awarded.Count} prior achievement(s) to new char {characterName} on account {accountId}.");

                foreach (var ach in awarded)
                    AchievementService.ApplyToCharacter(offline, ach);
            });
        }
    }
}
