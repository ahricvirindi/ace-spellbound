using ACE.Mods.Spellbound.Model.Base;

namespace ACE.Mods.Spellbound.Model
{
    public class AwardedCharacterAchievement : BaseKeyedModel
    {
        public uint CharacterId { get; set; }
        public int AchievementId { get; set; }
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    }
}
