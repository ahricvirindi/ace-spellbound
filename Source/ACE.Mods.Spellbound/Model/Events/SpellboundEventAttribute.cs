using ACE.Mods.Spellbound.Model.Enumerations;

namespace ACE.Mods.Spellbound.Model.Events
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SpellboundEventAttribute : Attribute
    {
        public SpellboundEventTrigger Trigger { get; }

        /// Lower runs earlier. Default 100 leaves room on either side for award-then-apply
        /// style ordering without renumbering everything.
        public int Order { get; init; } = 100;

        public SpellboundEventAttribute(SpellboundEventTrigger trigger)
        {
            Trigger = trigger;
        }
    }
}
