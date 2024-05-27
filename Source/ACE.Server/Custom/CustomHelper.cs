using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Custom
{
    internal static class CustomHelper
    {
        public static void Debug(Session session, string message)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat($"[DEBUG] {message}", ChatMessageType.System));
        }
    }
}
