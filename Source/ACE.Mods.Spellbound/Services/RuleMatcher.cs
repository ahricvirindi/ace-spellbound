using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;
using ACE.Mods.Spellbound.Model.Events.Payloads;

namespace ACE.Mods.Spellbound.Services
{
    public static class RuleMatcher
    {
        public static bool Matches(EventFilterType filterType, string? target, SpellboundEventArgs payload)
        {
            if (string.IsNullOrWhiteSpace(target))
                return true;

            return payload switch
            {
                PlayerKillEvent k => MatchesCreature(filterType, target, k.Victim),
                PlayerLevelEvent l => MatchesLevel(filterType, target, l),
                PlayerDeathEvent d => d.Killer != null && MatchesCreature(filterType, target, d.Killer),
                _ => false,
            };
        }

        private static bool MatchesCreature(EventFilterType filterType, string target, Creature victim)
        {
            switch (filterType)
            {
                case EventFilterType.WeenieId:
                    return uint.TryParse(target, out var wcid) && victim.WeenieClassId == wcid;

                case EventFilterType.CreatureType:
                    return Enum.TryParse<CreatureType>(target, ignoreCase: true, out var ct)
                           && victim.CreatureType == ct;

                // ItemType / QuestId / Level don't apply to a creature subject.
                default:
                    return false;
            }
        }

        private static bool MatchesLevel(EventFilterType filterType, string target, PlayerLevelEvent e)
        {
            if (!int.TryParse(target, out var threshold))
                return false;

            return filterType switch
            {
                EventFilterType.Level => e.ToLevel == threshold,
                EventFilterType.LevelMin => e.ToLevel >= threshold,
                _ => false,
            };
        }
    }
}
