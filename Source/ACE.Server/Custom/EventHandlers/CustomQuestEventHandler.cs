using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Custom.EventHandlers
{
    internal static class CustomQuestEventHandler
    {
        public static void OnStart(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomQuestEventHandler.OnStart()");
        }

        public static void OnIncrement(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomQuestEventHandler.OnIncrement()");
        }

        public static void OnCompletion(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomQuestEventHandler.OnCompletion()");
        }
    }
}
