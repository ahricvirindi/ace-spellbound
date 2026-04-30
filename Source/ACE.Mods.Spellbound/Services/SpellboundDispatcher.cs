using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;

namespace ACE.Mods.Spellbound.Services
{
    public static class SpellboundDispatcher
    {
        public static void Run(
            SpellboundContext db,
            SpellboundEventTrigger trigger,
            SpellboundEventArgs payload,
            uint accountId)
        {
            foreach (var ach in RuleEvaluator.MatchAchievements(db, trigger, payload))
                AchievementService.Award(db, accountId, ach);

            foreach (var rule in RuleEvaluator.MatchWorldStateRules(db, trigger, payload))
            {
                SpellboundLog.Info(
                    $"WorldState rule '{rule.Name}' matched on {trigger}; advancing town {rule.TownId} → stage {rule.TargetStage}.");
                WorldStateService.AdvanceTownStage(rule.TownId, rule.TargetStage);
            }

            CustomAchievementRegistry.EvaluateForTrigger(trigger, payload, accountId, db);
        }
    }
}
