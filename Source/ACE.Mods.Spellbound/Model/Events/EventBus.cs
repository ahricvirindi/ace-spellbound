using System.Linq.Expressions;

using ACE.Mods.Spellbound.Model.Enumerations;
using ACE.Mods.Spellbound.Model.Events.Payloads;

namespace ACE.Mods.Spellbound.Model.Events
{
    public static class EventBus
    {
        private static readonly IReadOnlyDictionary<SpellboundEventTrigger, Type> _payloadFor =
            new Dictionary<SpellboundEventTrigger, Type>
            {
                [SpellboundEventTrigger.Player_OnDeath] = typeof(PlayerDeathEvent),
                [SpellboundEventTrigger.Player_OnKill] = typeof(PlayerKillEvent),
                [SpellboundEventTrigger.Player_OnLevel] = typeof(PlayerLevelEvent),
                [SpellboundEventTrigger.Player_PreCast] = typeof(PlayerPreCastEvent),
            };

        internal sealed record Subscriber(
            SpellboundEventTrigger Trigger,
            int Order,
            string DisplayName,
            Action<SpellboundEventArgs> Invoke);

        private static readonly TriggerKeyedRegistry<Subscriber> _registry = new("EventBus");

        public static Type? PayloadFor(SpellboundEventTrigger trigger)
            => _payloadFor.TryGetValue(trigger, out var t) ? t : null;

        public static void DiscoverAndRegister(Assembly assembly)
        {
            _registry.Register<SpellboundEventAttribute>(
                assembly,
                TryBuildSubscriber,
                triggerSelector: s => s.Trigger,
                orderEntries: subs => subs.OrderBy(s => s.Order));
        }

        public static void Reset() => _registry.Reset();

        public static void Publish<T>(SpellboundEventTrigger trigger, T payload) where T : SpellboundEventArgs
        {
            var list = _registry.GetForTrigger(trigger);
            if (list.Count == 0)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                var sub = list[i];
                try
                {
                    sub.Invoke(payload);
                }
                catch (Exception ex)
                {
                    SpellboundLog.Error($"Subscriber {sub.DisplayName} for {trigger} threw: {ex}");
                }
            }
        }

        private static (Subscriber? entry, string? error) TryBuildSubscriber(
            MethodInfo method,
            SpellboundEventAttribute attr)
        {
            var displayName = $"{method.DeclaringType?.FullName}.{method.Name}";

            if (!method.IsStatic)
                return (null, $"{displayName}: must be static.");
            if (method.ReturnType != typeof(void))
                return (null, $"{displayName}: must return void.");

            var ps = method.GetParameters();
            if (ps.Length != 1)
                return (null, $"{displayName}: must take exactly one parameter.");

            var paramType = ps[0].ParameterType;
            if (!typeof(SpellboundEventArgs).IsAssignableFrom(paramType))
                return (null, $"{displayName}: parameter must derive from SpellboundEventArgs (got {paramType.Name}).");

            // Trigger MUST be in the canonical payload map. Forces every new trigger to
            // be declared up-front so registration is the single point of validation.
            if (!_payloadFor.TryGetValue(attr.Trigger, out var canonical))
                return (null, $"{displayName}: trigger {attr.Trigger} has no canonical payload registered in EventBus._payloadFor.");

            if (!paramType.IsAssignableFrom(canonical))
                return (null, $"{displayName}: parameter {paramType.Name} cannot receive payload {canonical.Name} for trigger {attr.Trigger}.");

            // Compile: (SpellboundEventArgs p) => method((TPayload)p)
            var p = Expression.Parameter(typeof(SpellboundEventArgs), "p");
            var call = Expression.Call(method, Expression.Convert(p, paramType));
            var invoker = Expression.Lambda<Action<SpellboundEventArgs>>(call, p).Compile();

            return (new Subscriber(attr.Trigger, attr.Order, displayName, invoker), null);
        }
    }
}
