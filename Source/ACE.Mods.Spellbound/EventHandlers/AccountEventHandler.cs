using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Spellbound.EventHandlers
{
    public class AccountEventHandler
    {
        public static void OnCreate()
        {
            ModManager.Log("Spellbound.OnCreate()");
        }

        public static void OnLogin()
        {
            ModManager.Log("Spellbound.OnLogin()");
        }
    }
}
