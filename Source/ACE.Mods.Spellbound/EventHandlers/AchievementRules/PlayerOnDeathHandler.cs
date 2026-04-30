using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Model.Events.Payloads;
using ACE.Mods.Spellbound.Services;

namespace ACE.Mods.Spellbound.EventHandlers.AchievementRules
{
    [HarmonyPatch]
    public sealed class PlayerOnDeathHandler : SpellboundPatchBase
    {
        public PlayerOnDeathHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnDeath),
            new[] { typeof(DamageHistoryInfo), typeof(DamageType), typeof(bool) })]
        public static void PublishPlayerDeath(
            DamageHistoryInfo lastDamager,
            DamageType damageType,
            bool criticalHit,
            Player __instance)
        {
            // Null lastDamager (environment, fall, DoT-after-source-died) is a real
            // PvE death; we still publish, payload's Killer is null and a non-wildcard
            // matcher will return false for that row.
            var attacker = lastDamager?.TryGetAttacker();
            if (attacker is Player) return; // PvP — not our trigger.

            EventBus.Publish(
                SpellboundEventTrigger.Player_OnDeath,
                new PlayerDeathEvent(__instance, attacker as Creature, damageType));
        }

        [SpellboundEvent(SpellboundEventTrigger.Player_OnDeath)]
        public static void OnPlayerDeath(PlayerDeathEvent e)
        {
            if (e.AccountId is not uint accountId) return;
            RunDbWork(db => SpellboundDispatcher.Run(db, SpellboundEventTrigger.Player_OnDeath, e, accountId));
        }
    }
}
