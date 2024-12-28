using ACE.Mods.Spellbound.Model.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
