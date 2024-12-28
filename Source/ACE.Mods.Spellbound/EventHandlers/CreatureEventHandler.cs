using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Spellbound.EventHandlers
{
    public class CreatureEventHandler
    {
        public void OnDeath()
        {
            ModManager.Log("Spellbound.OnDeath()");
        }
    }
}
