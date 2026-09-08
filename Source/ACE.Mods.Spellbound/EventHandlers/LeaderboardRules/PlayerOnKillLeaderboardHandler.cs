using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Model.Events.Payloads;
using ACE.Mods.Spellbound.Services;

namespace ACE.Mods.Spellbound.EventHandlers.LeaderboardRules
{
    // Leaderboard counter for kills, keyed by creature type.
    //
    // Why this is a separate subscriber from PlayerOnKillHandler's
    // achievement dispatch: leaderboards are independent from achievements
    // (different write target, different cadence, different lifecycle —
    // truncated on season wipe while AccountAchievement awards survive).
    // CLAUDE.md explicitly carves leaderboards out as one of the legitimate
    // "second [SpellboundEvent] subscriber per trigger" exceptions.
    //
    // Performance: kills are infrequent enough that a per-event UPSERT is
    // fine. The captured fields (charId, accountId, name, creature type) are
    // read off the patch thread before RunDbWork dispatches the SQL — by the
    // time the threadpool work runs, the Victim biota may already be in
    // teardown, and reading Victim.CreatureType then would be unsafe.
    [HarmonyPatch]
    public sealed class PlayerOnKillLeaderboardHandler : SpellboundPatchBase
    {
        public PlayerOnKillLeaderboardHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [SpellboundEvent(SpellboundEventTrigger.Player_OnKill)]
        public static void OnPlayerKill(PlayerKillEvent e)
        {
            if (e.Killer?.Account == null) return;
            if (e.Victim?.CreatureType is not CreatureType creatureType) return;

            // Don't record kills for elevated accounts (Advocate / Sentinel /
            // Envoy / Developer / Admin) — they'd dominate any leaderboard
            // they touched and most kills they make are during testing /
            // moderation, not gameplay. Player = 0 is the only level we
            // record; everything else early-returns. Mirrored in the web
            // LeaderboardService read path as defense in depth.
            if ((AccessLevel)e.Killer.Account.AccessLevel > AccessLevel.Player) return;

            var characterId = e.Killer.Guid.Full;
            var accountId = (int)e.Killer.Account.AccountId;
            var characterName = e.Killer.Name ?? string.Empty;
            var target = creatureType.ToString();

            RunDbWork(db => LeaderboardService.RecordEvent(
                db,
                LeaderboardCategory.KillsByCreature,
                target,
                characterId,
                accountId,
                characterName));
        }
    }
}
