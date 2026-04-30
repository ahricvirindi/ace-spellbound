using ACE.Mods.Spellbound.Model.Base;

namespace ACE.Mods.Spellbound.Model
{
    public class LandblockAlias : BaseNamedModel
    {
        public string Landblock { get; set; } = string.Empty;
        public uint? SetByAccountId { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
