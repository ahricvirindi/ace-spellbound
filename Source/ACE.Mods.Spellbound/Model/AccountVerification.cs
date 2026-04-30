using ACE.Mods.Spellbound.Model.Base;

namespace ACE.Mods.Spellbound.Model
{
    public class AccountVerification : BaseKeyedModel
    {
        public int AccountId { get; set; }
        public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
    }
}
