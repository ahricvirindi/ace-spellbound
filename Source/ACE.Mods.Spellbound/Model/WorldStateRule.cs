using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ACE.Mods.Spellbound.Model.Base;
using ACE.Mods.Spellbound.Model.Enumerations;

namespace ACE.Mods.Spellbound.Model
{
    public class WorldStateRule : BaseNamedModel
    {
        public SpellboundEventTrigger EventTrigger { get; set; }

        public EventFilterType FilterType { get; set; }

        // Free-form filter value; meaning is determined by FilterType. Null/empty = wildcard.
        public string? Target { get; set; }

        public int ZoneId { get; set; }

        [ForeignKey(nameof(ZoneId))]
        public Zone? Zone { get; set; }

        public int TargetStage { get; set; }

        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}
