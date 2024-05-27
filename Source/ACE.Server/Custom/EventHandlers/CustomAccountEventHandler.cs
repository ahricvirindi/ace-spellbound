using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Custom.EventHandlers
{
    internal static class CustomAccountEventHandler
    {
        public static void OnCreate(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomAccountEventHandler.OnCreate()");
        }

        public static void OnLogin(Network.Session session)
        {
            CustomHelper.Debug(session, "CustomAccountEventHandler.OnLogin()");
        }
    }
}
