using ACE.Server.Custom.Data.Base;
using ACE.Server.Custom.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Custom.Data
{
    public class Achievement : BaseNamedModel
    {
        public AchievementTriggers AchievemntTrigger { get; set; }
        public string AwardDescription { get; set; }
        public AchievementTargetTypes AchievementTargetType { get; set; }

        // this could be the name of a spell, a creature type, a quest name, things like that
        public string? Target { get; set; }

        public AchivementAwardTypes AwardType { get; set; }
        public int? AwardValue { get; set; }
        public int? AmountRequired { get; set; }
    }
}
