using ACE.Server.Custom.Data.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Custom.Data
{
    public class AccountVerification : BaseKeyedModel
    {
        public int AccountId { get; set; }
        public DateTime VerifiedAt { get; set; } = DateTime.Now;
    }
}
