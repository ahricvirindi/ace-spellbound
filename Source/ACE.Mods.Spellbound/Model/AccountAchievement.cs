using ACE.Mods.Spellbound.Model.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Mods.Spellbound.Model
{
    public class AccountAchievement : BaseKeyedModel
    {
        public int AccountId { get; set; }
        public int AchievementId { get; set; }

        [ForeignKey("AchievementId")]
        public Achievement? Achievement { get; set; }

        public int Progress { get; set; }
        public DateTime? AwardedAt { get; set; }

        // Optimistic-concurrency token. Bump on every update via a SaveChanges interceptor
        // or by manually incrementing inside the transaction that mutates the row.
        // Pomelo/MySQL doesn't support SQL-Server-style rowversion, so we use a manual int.
        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}
