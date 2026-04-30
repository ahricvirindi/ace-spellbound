using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.EventHandlers.CustomAchievementRules.Enumerations;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Model.Events.Payloads;

namespace ACE.Mods.Spellbound.EventHandlers.CustomAchievementRules
{
    // THIS IS JUST AN EXAMPLE
    public static class FirstCriticalKill
    {
        [CustomAchievement((int)CustomAchievements.FIRST_CRIT_KILL, SpellboundEventTrigger.Player_OnKill)]
        public static bool Evaluate(PlayerKillEvent e, uint accountId, SpellboundContext db)
        {
            return e.CriticalHit;
        }
    }
}
