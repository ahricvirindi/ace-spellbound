using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using static ACE.Server.WorldObjects.Player;

namespace ACE.Server.Custom.EventHandlers
{
    internal static class CustomPlayerEventHandler
    {
        public static CastingPreCheckStatus OnCastFizzleCheck(Network.Session session, Entity.Spell spell, CastingPreCheckStatus status)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnCastFizzleCheck()");

            if (spell.School != MagicSchool.ItemEnchantment)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"The energies from your casting nearly coalesce and then fizzle to nothing.  Something unnatural blocks incantations of that type.", ChatMessageType.Magic));
                status = CastingPreCheckStatus.CastFailed;
            }

            return status;
        }

        public static void OnCreate(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnCreate()");
        }

        public static void OnLogin(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnLogin()");
        }

        public static void OnDeath(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnDeath()");
        }

        public static void OnPortalEntry(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnPortalEntry()");
        }

        public static void OnPortalExit(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnPortalExit()");
        }

        public static void OnLevel(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnLevel()");
        }

        public static void OnDamageTaken(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnDamageTaken()");
        }

        public static void OnDamageGiven(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnDamageTaken()");
        }

        public static void OnCritDamageTaken(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnCritDamageTaken()");
        }

        public static void OnCritDamageGiven(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnCritDamageTaken()");
        }

        public static void OnMeleeEvade(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnMeleeEvade()");
        }

        public static void OnMagicResist(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnMagicResist()");
        }

        public static void OnLifeRegen(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnLifeRegen()");
        }

        public static void OnManaRegen(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnManaRegen()");
        }

        public static void OnStaminaRegen(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnStaminaRegen()");
        }

        public static void OnKill(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnKill()");
        }

        public static void OnDamageGivenCalculating(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnKill()");
        }

        public static void OnDamageTakenCalculating(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnKill()");
        }

        public static void OnExperienceAwarding(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnKill()");
        }

        public static void OnLuminanceAwarding(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomPlayerEventHandler.OnKill()");
        }
    }
}
