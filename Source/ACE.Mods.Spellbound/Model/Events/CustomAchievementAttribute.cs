using ACE.Mods.Spellbound.Model.Enumerations;

namespace ACE.Mods.Spellbound.Model.Events
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class CustomAchievementAttribute : Attribute
    {
        public int AchievementId { get; }
        public SpellboundEventTrigger Trigger { get; }

        public CustomAchievementAttribute(int achievementId, SpellboundEventTrigger trigger)
        {
            AchievementId = achievementId;
            Trigger = trigger;
        }
    }
}
