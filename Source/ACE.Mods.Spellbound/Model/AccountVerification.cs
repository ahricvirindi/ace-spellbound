using ACE.Mods.Spellbound.Model.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Spellbound.Model
{
    public class AccountVerification : BaseKeyedModel
    {
        public int AccountId { get; set; }
        public DateTime VerifiedAt { get; set; } = DateTime.Now;
    }
}
