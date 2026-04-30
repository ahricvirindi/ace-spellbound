using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Model.Events.Payloads;
using ACE.Mods.Spellbound.Services;

using static ACE.Server.WorldObjects.Player;

namespace ACE.Mods.Spellbound.EventHandlers.AchievementRules
{
    /// <summary>
    /// Single point of integration for <see cref="SpellboundEventTrigger.Player_PreCast"/>.
    /// Owns the Harmony publisher (postfix on <see cref="Player.GetCastingPreCheckStatus"/>)
    /// and a custom-achievement dispatcher subscriber. Gameplay rules that want to
    /// block a cast subscribe to the same trigger and set
    /// <see cref="PlayerPreCastEvent.CancelCast"/>; the publisher translates that
    /// back into <c>CastingPreCheckStatus.CastFailed</c> on the upstream <c>ref</c>
    /// result, on the original casting thread.
    /// </summary>
    [HarmonyPatch]
    public sealed class PlayerPreCastHandler : SpellboundPatchBase
    {
        public PlayerPreCastHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.GetCastingPreCheckStatus),
            new[] { typeof(Spell), typeof(uint), typeof(bool) })]
        public static void PublishPreCast(
            Spell spell,
            uint magicSkill,
            bool isWeaponSpell,
            Player __instance,
            ref CastingPreCheckStatus __result)
        {
            // Don't second-guess upstream rejections — once the engine has decided
            // the cast cannot proceed, our subscribers have nothing useful to do.
            if (__result == CastingPreCheckStatus.CastFailed)
                return;

            var payload = new PlayerPreCastEvent(__instance, spell, magicSkill, isWeaponSpell);
            EventBus.Publish(SpellboundEventTrigger.Player_PreCast, payload);

            if (payload.CancelCast)
                __result = CastingPreCheckStatus.CastFailed;
        }

        // Order = 200 so any gameplay-rule subscriber (default 100) has already had
        // a chance to flip CancelCast — no point running achievement / world-state
        // dispatch for a cast that's about to fail.
        [SpellboundEvent(SpellboundEventTrigger.Player_PreCast, Order = 200)]
        public static void OnPreCast(PlayerPreCastEvent e)
        {
            if (e.CancelCast) return;
            if (e.AccountId is not uint accountId) return;
            RunDbWork(db => SpellboundDispatcher.Run(db, SpellboundEventTrigger.Player_PreCast, e, accountId));
        }
    }
}
