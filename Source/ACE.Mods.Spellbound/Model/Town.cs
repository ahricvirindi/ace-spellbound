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
    public class Town : BaseNamedModel
    {
        public string Landblock { get; set; } = string.Empty;
        public int Stage { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
