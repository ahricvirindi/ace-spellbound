using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Spellbound.EventHandlers
{
    public class QuestEventHandler
    {
        public void OnStart()
        {
            ModManager.Log("Spellbound.OnStart()");
        }

        public void OnIncrement()
        {
            ModManager.Log("Spellbound.OnIncrement()");
        }

        public void OnCompletion()
        {
            ModManager.Log("Spellbound.OnCompletion()");
        }
    }
}
