using ACE.Mods.Spellbound.Model.Base;
using System.ComponentModel.DataAnnotations;

namespace ACE.Mods.Spellbound.Model
{
    public class Town : BaseNamedModel
    {
        public string Landblock { get; set; } = string.Empty;
        public int Stage { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}
