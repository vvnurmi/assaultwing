﻿using System.Collections.Generic;
using System.Linq;

namespace AW2.Net.ManagementMessages
{
    /// <summary>
    /// A message from a management server to a game client, telling if joining a game server
    /// is possible and if so, then how to do it.
    /// </summary>
    [ManagementMessage("serveraddress")]
    public class JoinGameServerReply : ManagementMessage
    {
        public bool Success { get { return GameServerEndPoints != null; } }
        public AWEndPoint[] GameServerEndPoints { get; private set; }
        public string FailMessage { get; private set; }

        protected override void Deserialize(List<Dictionary<string, string>> tokenizedLines)
        {
            var tokens = tokenizedLines[0];
            if (tokens.ContainsKey("fail"))
            {
                FailMessage = tokens["fail"];
            }
            else
            {
                string endPointString1 = tokens["server"];
                string endPointString2 = tokens["server2"];
                GameServerEndPoints = new AWEndPoint[]
                {
                    AWEndPoint.Parse(endPointString1),
                    AWEndPoint.Parse(endPointString2),
                };
            }
        }
    }
}
