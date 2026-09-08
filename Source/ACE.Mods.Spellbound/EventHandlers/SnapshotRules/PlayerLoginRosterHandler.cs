using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Services;

namespace ACE.Mods.Spellbound.EventHandlers.SnapshotRules
{
    // Inserts/updates an OnlinePlayers row on PlayerEnterWorld. Symmetric with
    // PlayerLogoutSnapshotHandler; together with the 5-min rebuild timer
    // (SnapshotTimers) they keep the /who snapshot fresh.
    //
    // Why no EventBus: this is a snapshot lifecycle hook, not an event with
    // achievement-rule dispatch. Same pattern as PlayerOnEnterWorldHandler
    // (which does the achievement re-walk on login).
    [HarmonyPatch]
    public sealed class PlayerLoginRosterHandler : SpellboundPatchBase
    {
        public PlayerLoginRosterHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
        public static void OnEnterWorld(Player __instance)
        {
            if (__instance?.Account == null) return;

            // Capture row fields on the calling thread; landblock-name resolution
            // happens inside RunDbWork where we have a context.
            var row = SnapshotService.BuildOnlineRow(__instance);

            RunDbWork(db => SnapshotService.WriteOnline(db, row));
        }
    }
}
