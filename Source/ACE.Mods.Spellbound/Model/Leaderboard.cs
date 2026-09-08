using ACE.Mods.Spellbound.Model.Base;
using ACE.Mods.Spellbound.Model.Enumerations;

namespace ACE.Mods.Spellbound.Model
{
    // Per-character event-driven counter row. Unique on (Category, Target,
    // CharacterId) — see Database/Spellbound/Updates/2026-04-30-006-add-leaderboards.sql.
    //
    // LeaderboardService is the only sanctioned writer; everything else reads
    // through the web LeaderboardService.
    //
    // Target is intentionally NOT NULL DEFAULT '' — MySQL treats NULL as
    // distinct in unique keys, which would silently allow duplicate
    // (Category, char) rows for category-wide leaderboards. Empty-string
    // sentinel keeps the unique constraint honest.
    public class Leaderboard : BaseKeyedModel
    {
        public LeaderboardCategory Category { get; set; }
        public string Target { get; set; } = string.Empty;
        public uint CharacterId { get; set; }
        public int AccountId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public long Count { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
