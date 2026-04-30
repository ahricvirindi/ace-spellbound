using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Services;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.EventHandlers.AchievementRules
{
    /// <summary>
    /// Login-time safety net for missed bonus applications. For every
    /// AccountAchievement granted to this player's account that has no
    /// matching AwardedCharacterAchievements row for this character, re-run
    /// ApplyToCharacter so the bonus lands.
    ///
    /// Why this exists: ApplyToCharacter is two steps — INSERT IGNORE the
    /// idempotency row, then mutate the character's stat. If the mutation
    /// throws after the insert, the character ends up with a "phantom" row
    /// and no bonus. The on-grant walk and the on-character-create walk both
    /// run once and don't retry. Re-checking on every login is cheap (one
    /// LEFT JOIN per login) and the only path that catches achievements
    /// granted while the character was offline AND missed by the on-grant
    /// walk for any reason (race, transient crash, character not yet
    /// registered with PlayerManager).
    ///
    /// Why no EventBus: this is a lifecycle hook, not an event with
    /// rule-driven dispatch. Following the same pattern as
    /// PlayerOnCharacterCreatedHandler — plain [HarmonyPatch], no
    /// [SpellboundEvent] subscriber.
    ///
    /// Note this only catches the "no apply row at all" case. The "phantom
    /// row" case (apply row exists but stat was never bumped) still requires
    /// the manual recovery flagged by ApplyToCharacter's error log.
    /// </summary>
    [HarmonyPatch]
    public sealed class PlayerOnEnterWorldHandler : SpellboundPatchBase
    {
        public PlayerOnEnterWorldHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
        public static void OnEnterWorld(Player __instance)
        {
            if (__instance?.Account == null) return;

            var accountId = (uint)__instance.Account.AccountId;
            var characterGuid = __instance.Guid.Full;
            var characterName = __instance.Name;

            RunDbWork(db =>
            {
                // Granted achievements for this account whose apply row is missing for this character.
                // LEFT JOIN over CharacterAchievements with a NULL filter is the cheapest way
                // to express "set difference" in EF Core / MySQL — single round trip, single index hit
                // on (CharacterId, AchievementId).
                var missing = (from aa in db.AccountAchievements.AsNoTracking()
                               join ach in db.Achievements.AsNoTracking() on aa.AchievementId equals ach.Id
                               where aa.AccountId == (int)accountId
                                     && aa.AwardedAt != null
                                     && !db.CharacterAchievements.Any(ac =>
                                            ac.CharacterId == characterGuid
                                            && ac.AchievementId == aa.AchievementId)
                               select ach).ToList();

                if (missing.Count == 0)
                    return;

                SpellboundLog.Info(
                    $"Login safety net: re-applying {missing.Count} missing achievement(s) to {characterName} ({characterGuid:X8}) on account {accountId}.");

                foreach (var ach in missing)
                    AchievementService.ApplyToCharacter(__instance, ach);
            });
        }
    }
}
