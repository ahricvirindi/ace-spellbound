using ACE.Mods.Spellbound.Model.Base;
using ACE.Mods.Spellbound.Model.Enumerations;

namespace ACE.Mods.Spellbound.Model
{
    public class Achievement : BaseNamedModel
    {
        public SpellboundEventTrigger EventTrigger { get; set; }
        public string AwardDescription { get; set; } = string.Empty;
        public EventFilterType FilterType { get; set; }

        // Free-form filter value; meaning is determined by FilterType
        // (e.g. weenie id, creature type name, quest name).
        public string? Target { get; set; }

        public AchievementAwardTypes AwardType { get; set; }
        public int? AwardValue { get; set; }
        public int? AmountRequired { get; set; }
    }
}
