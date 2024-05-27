using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Custom.Data.Base
{
    public class BaseNamedModel : BaseKeyedModel
    {
        public string Name { get; set; }
    }
}
