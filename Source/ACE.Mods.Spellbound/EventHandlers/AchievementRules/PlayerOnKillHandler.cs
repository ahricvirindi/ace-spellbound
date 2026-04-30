using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Model.Events.Payloads;
using ACE.Mods.Spellbound.Services;

namespace ACE.Mods.Spellbound.EventHandlers.AchievementRules
{
    [HarmonyPatch]
    public sealed class PlayerOnKillHandler : SpellboundPatchBase
    {
        public PlayerOnKillHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Creature), nameof(Creature.OnDeath),
            new[] { typeof(DamageHistoryInfo), typeof(DamageType), typeof(bool) })]
        public static void PublishPlayerKill(
            DamageHistoryInfo lastDamager,
            DamageType damageType,
            bool criticalHit,
            Creature __instance)
        {
            if (__instance is Player) return;
            if (lastDamager == null) return;

            var attacker = lastDamager.TryGetAttacker();
            if (attacker is not Player killer) return;

            EventBus.Publish(
                SpellboundEventTrigger.Player_OnKill,
                new PlayerKillEvent(killer, __instance, damageType, criticalHit));
        }

        [SpellboundEvent(SpellboundEventTrigger.Player_OnKill)]
        public static void OnPlayerKill(PlayerKillEvent e)
        {
            if (e.AccountId is not uint accountId) return;
            RunDbWork(db => SpellboundDispatcher.Run(db, SpellboundEventTrigger.Player_OnKill, e, accountId));
        }
    }
}
