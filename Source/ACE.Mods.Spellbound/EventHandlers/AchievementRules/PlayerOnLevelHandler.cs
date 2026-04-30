using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Model.Events.Payloads;
using ACE.Mods.Spellbound.Services;

namespace ACE.Mods.Spellbound.EventHandlers.AchievementRules
{
    /// <summary>
    /// Single point of integration for <see cref="SpellboundEventTrigger.Player_OnLevel"/>.
    /// Owns the Harmony prefix (captures the pre-levelup level) + postfix
    /// (publishes one event per level gained) + the EventBus subscriber that
    /// dispatches to services.
    ///
    /// Why prefix + postfix instead of just postfix: <c>Player.CheckForLevelup</c>
    /// stores the pre-levelup value in a local <c>startingLevel</c> that isn't
    /// reachable from a postfix. Harmony's <c>__state</c> mechanism lets the
    /// prefix capture it and the postfix read it, which is cheaper than a
    /// transpiler and more reliable than caching <c>Level</c> in a static dict
    /// keyed by player.
    ///
    /// Why one event per level: a 49→52 XP grant calls
    /// <c>CheckForLevelup</c> once, with <c>Level</c> incrementing three times
    /// internally. Firing once per level lets achievement rules like "reach
    /// level 50" match naturally without ranges.
    /// </summary>
    [HarmonyPatch]
    public sealed class PlayerOnLevelHandler : SpellboundPatchBase
    {
        public PlayerOnLevelHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "CheckForLevelup")]
        public static void CapturePreLevel(Player __instance, out int __state)
        {
            __state = __instance.Level ?? 1;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "CheckForLevelup")]
        public static void PublishLevelup(Player __instance, int __state)
        {
            var oldLevel = __state;
            var newLevel = __instance.Level ?? 1;
            if (newLevel <= oldLevel) return;

            for (int lvl = oldLevel + 1; lvl <= newLevel; lvl++)
            {
                EventBus.Publish(
                    SpellboundEventTrigger.Player_OnLevel,
                    new PlayerLevelEvent(__instance, lvl - 1, lvl));
            }
        }

        [SpellboundEvent(SpellboundEventTrigger.Player_OnLevel)]
        public static void OnPlayerLevel(PlayerLevelEvent e)
        {
            if (e.AccountId is not uint accountId) return;
            RunDbWork(db => SpellboundDispatcher.Run(db, SpellboundEventTrigger.Player_OnLevel, e, accountId));
        }
    }
}
