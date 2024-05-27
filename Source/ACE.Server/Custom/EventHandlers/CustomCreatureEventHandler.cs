using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ACE.Server.WorldObjects.Player;

namespace ACE.Server.Custom.EventHandlers
{
    internal static class CustomCreatureEventHandler
    {
        public static void OnDeath(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomCreatureEventHandler.OnDeath()");
        }
    }
}
