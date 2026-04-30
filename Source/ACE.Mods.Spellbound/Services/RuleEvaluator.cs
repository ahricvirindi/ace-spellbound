using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Model;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;

using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Spellbound.Services
{
    public static class RuleEvaluator
    {
        public static IReadOnlyList<Achievement> MatchAchievements(
            SpellboundContext db,
            SpellboundEventTrigger trigger,
            SpellboundEventArgs payload)
        {
            var candidates = db.Achievements
                .AsNoTracking()
                .Where(a => a.EventTrigger == trigger)
                .ToList();

            var matches = new List<Achievement>(candidates.Count);
            foreach (var a in candidates)
                if (RuleMatcher.Matches(a.FilterType, a.Target, payload))
                    matches.Add(a);

            return matches;
        }

        public static IReadOnlyList<WorldStateRule> MatchWorldStateRules(
            SpellboundContext db,
            SpellboundEventTrigger trigger,
            SpellboundEventArgs payload)
        {
            var candidates = db.WorldStateRules
                .AsNoTracking()
                .Where(r => r.EventTrigger == trigger)
                .ToList();

            var matches = new List<WorldStateRule>(candidates.Count);
            foreach (var r in candidates)
                if (RuleMatcher.Matches(r.FilterType, r.Target, payload))
                    matches.Add(r);

            return matches;
        }
    }
}
