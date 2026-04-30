using System.Linq.Expressions;

using ACE.Mods.Spellbound.Data;
using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events;

namespace ACE.Mods.Spellbound.Services
{
    public static class CustomAchievementRegistry
    {
        internal sealed record Evaluator(
            int AchievementId,
            SpellboundEventTrigger Trigger,
            string DisplayName,
            Func<SpellboundEventArgs, uint, SpellboundContext, bool> Invoke);

        private static readonly TriggerKeyedRegistry<Evaluator> _registry = new("CustomAchievementRegistry");

        public static void DiscoverAndRegister(Assembly assembly)
        {
            _registry.Register<CustomAchievementAttribute>(
                assembly,
                TryBuildEvaluator,
                triggerSelector: e => e.Trigger);
        }

        public static void Reset() => _registry.Reset();

        public static void EvaluateForTrigger(
            SpellboundEventTrigger trigger,
            SpellboundEventArgs payload,
            uint accountId,
            SpellboundContext db)
        {
            var list = _registry.GetForTrigger(trigger);
            if (list.Count == 0)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                var ev = list[i];
                try
                {
                    if (ev.Invoke(payload, accountId, db))
                        AchievementService.AwardById(db, accountId, ev.AchievementId);
                }
                catch (Exception ex)
                {
                    SpellboundLog.Error($"Custom achievement {ev.DisplayName} for {trigger} threw: {ex}");
                }
            }
        }

        private static (Evaluator? entry, string? error) TryBuildEvaluator(
            MethodInfo method,
            CustomAchievementAttribute attr)
        {
            var displayName = $"{method.DeclaringType?.FullName}.{method.Name}";

            if (!method.IsStatic)
                return (null, $"{displayName}: must be static.");
            if (method.ReturnType != typeof(bool))
                return (null, $"{displayName}: must return bool.");

            var ps = method.GetParameters();
            if (ps.Length != 3)
                return (null, $"{displayName}: must take exactly three parameters (TPayload, uint accountId, SpellboundContext db).");

            var payloadParamType = ps[0].ParameterType;
            if (!typeof(SpellboundEventArgs).IsAssignableFrom(payloadParamType))
                return (null, $"{displayName}: first parameter must derive from SpellboundEventArgs (got {payloadParamType.Name}).");
            if (ps[1].ParameterType != typeof(uint))
                return (null, $"{displayName}: second parameter must be uint accountId (got {ps[1].ParameterType.Name}).");
            if (ps[2].ParameterType != typeof(SpellboundContext))
                return (null, $"{displayName}: third parameter must be SpellboundContext db (got {ps[2].ParameterType.Name}).");

            var canonical = EventBus.PayloadFor(attr.Trigger);
            if (canonical == null)
                return (null, $"{displayName}: trigger {attr.Trigger} has no canonical payload registered in EventBus._payloadFor.");
            if (!payloadParamType.IsAssignableFrom(canonical))
                return (null, $"{displayName}: parameter {payloadParamType.Name} cannot receive payload {canonical.Name} for trigger {attr.Trigger}.");

            var pPayload = Expression.Parameter(typeof(SpellboundEventArgs), "p");
            var pAccount = Expression.Parameter(typeof(uint), "a");
            var pDb = Expression.Parameter(typeof(SpellboundContext), "d");
            var call = Expression.Call(method, Expression.Convert(pPayload, payloadParamType), pAccount, pDb);
            var invoker = Expression.Lambda<Func<SpellboundEventArgs, uint, SpellboundContext, bool>>(call, pPayload, pAccount, pDb).Compile();

            return (new Evaluator(attr.AchievementId, attr.Trigger, displayName, invoker), null);
        }
    }
}
