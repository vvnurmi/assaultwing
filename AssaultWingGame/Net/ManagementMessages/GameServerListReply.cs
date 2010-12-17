﻿using System.Collections.Generic;
using System.Linq;

namespace AW2.Net.ManagementMessages
{
    /// <summary>
    /// A message from a game instance to a management server, requesting a list
    /// of available game servers.
    /// </summary>
    [ManagementMessage("serverlist")]
    public class GameServerListReply : ManagementMessage
    {
        public List<GameServerInfo> GameServers { get; private set; }

        protected override void Deserialize(List<Dictionary<string, string>> tokenizedLines)
        {
            GameServers = tokenizedLines
                .Skip(1)
                .Select(line => GetGameServerInfo(line))
                .ToList();
        }

        private static GameServerInfo GetGameServerInfo(Dictionary<string, string> line)
        {
            return new GameServerInfo
            {
                Name = line["name"],
                ManagementID = int.Parse(line["id"]),
                CurrentPlayers = int.Parse(line["currentclients"]),
                MaxPlayers = int.Parse(line["maxclients"])
            };
        }
    }
}