namespace ACE.Mods.Spellbound.Model.Events
{
    using ACE.Mods.Spellbound.Model.Enumerations;

    internal sealed class TriggerKeyedRegistry<TEntry> where TEntry : class
    {
        private static readonly IReadOnlyDictionary<SpellboundEventTrigger, IReadOnlyList<TEntry>> _empty =
            new Dictionary<SpellboundEventTrigger, IReadOnlyList<TEntry>>();

        private readonly object _writeLock = new();
        private readonly string _displayName;
        private IReadOnlyDictionary<SpellboundEventTrigger, IReadOnlyList<TEntry>> _state = _empty;
        private bool _registered;

        public TriggerKeyedRegistry(string displayName)
        {
            _displayName = displayName;
        }

        public void Register<TAttribute>(
            Assembly assembly,
            Func<MethodInfo, TAttribute, (TEntry? entry, string? error)> tryBuild,
            Func<TEntry, SpellboundEventTrigger> triggerSelector,
            Func<IEnumerable<TEntry>, IEnumerable<TEntry>>? orderEntries = null)
            where TAttribute : Attribute
        {
            lock (_writeLock)
            {
                if (_registered)
                    return;

                var errors = new List<string>();
                var entries = new List<TEntry>();

                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsClass) continue;

                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        var attrs = method.GetCustomAttributes<TAttribute>(inherit: false).ToArray();
                        if (attrs.Length == 0) continue;

                        foreach (var attr in attrs)
                        {
                            var (entry, error) = tryBuild(method, attr);
                            if (error != null) errors.Add(error);
                            else if (entry != null) entries.Add(entry);
                        }
                    }
                }

                if (errors.Count > 0)
                    throw new InvalidOperationException(
                        $"Spellbound {_displayName} registration failed:\n  - " + string.Join("\n  - ", errors));

                var snapshot = new Dictionary<SpellboundEventTrigger, IReadOnlyList<TEntry>>();
                foreach (var grp in entries.GroupBy(triggerSelector))
                {
                    IEnumerable<TEntry> ordered = orderEntries != null ? orderEntries(grp) : grp;
                    snapshot[grp.Key] = ordered.ToArray();
                }

                Volatile.Write(ref _state, snapshot);
                _registered = true;

                foreach (var (trigger, list) in snapshot)
                    SpellboundLog.Info($"{_displayName}: {list.Count} entry(ies) for {trigger}");
            }
        }

        public IReadOnlyList<TEntry> GetForTrigger(SpellboundEventTrigger trigger)
        {
            var snap = Volatile.Read(ref _state);
            return snap.TryGetValue(trigger, out var list) ? list : Array.Empty<TEntry>();
        }

        public void Reset()
        {
            lock (_writeLock)
            {
                Volatile.Write(ref _state, _empty);
                _registered = false;
            }
        }
    }
}
