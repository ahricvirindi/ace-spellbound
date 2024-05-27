using ACE.Server.Custom.Data.Base;
using ACE.Server.Custom.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Custom.Data
{
    public class AccountAchievement : BaseKeyedModel
    {
        public int AccountId { get; set; }
        public int AchievementId { get; set; }

        [ForeignKey("AchievementId")]
        public Achievement Achievement { get; set; }

        public int Progress { get; set; }
        public DateTime? AwardedAt { get; set; }
    }
}
