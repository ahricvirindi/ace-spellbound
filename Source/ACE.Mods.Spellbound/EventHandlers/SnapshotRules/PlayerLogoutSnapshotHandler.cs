using ACE.Mods.Spellbound.Base;
using ACE.Mods.Spellbound.Model;
using ACE.Mods.Spellbound.Services;

namespace ACE.Mods.Spellbound.EventHandlers.SnapshotRules
{
    // FinalizeLogout is the canonical exit point for ALL logout paths (clean
    // logout, PK timer, forced logoff, network disconnect / timeout). It fires
    // exactly once per session teardown, with the Player still alive enough to
    // read attributes / skills / position. We read off the patch thread, then
    // dispatch the writes to the threadpool via RunDbWork so we don't block
    // the logout finalization.
    //
    // FinalizeLogout is private, so we name it as a string instead of using
    // nameof — which is fine, Harmony resolves private methods this way.
    [HarmonyPatch]
    public sealed class PlayerLogoutSnapshotHandler : SpellboundPatchBase
    {
        public PlayerLogoutSnapshotHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "FinalizeLogout")]
        public static void OnFinalizeLogout(Player __instance)
        {
            if (__instance?.Account == null) return;

            var characterId = __instance.Guid.Full;

            // Capture profile + equipment fields BEFORE returning — by the time
            // the threadpool work runs, the Player object may be torn down /
            // returned to the offline pool, and reading attributes off it
            // would NPE or read garbage.
            CharacterProfileSnapshot? profileRow = null;
            CharacterEquipmentSnapshot? equipmentRow = null;
            try
            {
                profileRow = SnapshotService.BuildProfileRow(__instance);
            }
            catch (Exception ex)
            {
                SpellboundLog.Error($"Failed to capture profile for {__instance.Name} on logout: {ex}");
            }
            try
            {
                equipmentRow = SnapshotService.BuildEquipmentRow(__instance);
            }
            catch (Exception ex)
            {
                SpellboundLog.Error($"Failed to capture equipment for {__instance.Name} on logout: {ex}");
            }

            RunDbWork(db =>
            {
                SnapshotService.RemoveOnline(db, characterId);
                if (profileRow != null)
                    SnapshotService.WriteProfile(db, profileRow);
                if (equipmentRow != null)
                    SnapshotService.WriteEquipment(db, equipmentRow);
            });
        }
    }
}
