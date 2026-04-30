using ACE.Mods.Spellbound.Base;
using ACE.Server.Network.Enum;
using ACE.Server.Network.Handlers;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.EventHandlers.AccountRules
{
    /// <summary>
    /// Enforces ReservedNames at character creation. Rejects creates of any
    /// name reserved to a different account (carried across season-wipe.sql by
    /// the pre-truncate snapshot block).
    ///
    /// Why prefix CharacterCreateEx and re-Unpack: upstream Unpacks the
    /// CharacterCreateInfo payload at the top of CharacterCreateEx, then runs
    /// a series of validation early-returns (taboo table, creature names,
    /// shard name availability). The cleanest place to drop another check in
    /// that pipeline is right before the existing ones — but we need the
    /// requested name, which only exists post-Unpack. We Unpack into a
    /// throwaway info, save/restore the underlying stream Position, and let
    /// upstream's own Unpack run normally if we don't reject. Character
    /// creation is rare (literal seconds-of-character-creation rate), so
    /// the double-Unpack cost is irrelevant.
    ///
    /// Why fail open on DB errors: a transient ReservedNames lookup failure
    /// shouldn't lock every player out of character creation. False rejection
    /// of a legit player is worse than a small race window where a snipe
    /// might slip through during a DB outage. We log loudly so post-incident
    /// review can spot any shoulder-tap'd reservation.
    /// </summary>
    [HarmonyPatch]
    public sealed class PlayerOnCreateReservedNameHandler : SpellboundPatchBase
    {
        public PlayerOnCreateReservedNameHandler(Mod mod, string settingsName) : base(mod, settingsName) { }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CharacterHandler), "CharacterCreateEx")]
        public static bool CheckReservedName(ClientMessage message, Session session)
        {
            string requestedName;
            var stream = message.Data;
            var savedPosition = stream.Position;
            try
            {
                var info = new CharacterCreateInfo();
                info.Unpack(message.Payload);
                requestedName = info.Name ?? string.Empty;
            }
            catch (Exception ex)
            {
                SpellboundLog.Warn(
                    $"CharacterCreate prefix: failed to peek name, deferring to upstream: {ex.Message}");
                return true;
            }
            finally
            {
                stream.Position = savedPosition;
            }

            if (string.IsNullOrWhiteSpace(requestedName))
                return true;

            var requestingAccountId = session.AccountId;

            try
            {
                using var db = CreateDbContext();
                var reservation = db.ReservedNames
                    .AsNoTracking()
                    .FirstOrDefault(r => r.Name == requestedName);

                if (reservation == null)
                    return true;
                if (reservation.AccountId == requestingAccountId)
                    return true;

                SpellboundLog.Info(
                    $"Blocked CharacterCreate of '{requestedName}' by account {requestingAccountId}: name reserved by account {reservation.AccountId}.");

                session.Network.EnqueueSend(
                    new GameMessageCharacterCreateResponse(
                        CharacterGenerationVerificationResponse.NameInUse,
                        default,
                        string.Empty));

                return false;
            }
            catch (Exception ex)
            {
                SpellboundLog.Error(
                    $"CharacterCreate prefix: ReservedNames lookup failed for '{requestedName}', failing open: {ex}");
                return true;
            }
        }
    }
}
