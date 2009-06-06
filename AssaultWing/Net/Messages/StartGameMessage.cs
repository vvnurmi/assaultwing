﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.IO;

namespace AW2.Net.Messages
{
    /// <summary>
    /// A message from a game server to a game client notifying
    /// that the game session starts. The message contains information
    /// about the player setup of the game session.
    /// </summary>
    /// To initialise a message for sending, call <c>BeginWrite</c> and
    /// serialise the gob's state with appropriate calls to the various
    /// write methods of the returned writer. Then call <c>EndWrite</c>
    /// and send the message.
    /// 
    /// To get the serialised data from a message, call <c>BeginRead</c>
    /// and deserialise the gob's state with appropriate calls to the various
    /// read methods of the returned reader. Then call <c>EndRead</c>.
    public class StartGameMessage : StreamMessage
    {
        /// <summary>
        /// Identifier of the message type.
        /// </summary>
        protected static MessageType messageType = new MessageType(0x21, false);

        /// <summary>
        /// Number of players in the game session.
        /// </summary>
        public int PlayerCount { get; set; }

        /// <summary>
        /// Names of arenas to play in the game session.
        /// </summary>
        public IList<string> ArenaPlaylist { get; set; }

        /// <summary>
        /// Creates an uninitialised start game message.
        /// </summary>
        public StartGameMessage()
        {
            ArenaPlaylist = new List<string>();
        }

        /// <summary>
        /// Writes the body of the message in serialised form.
        /// </summary>
        /// <param name="writer">Writer of serialised data.</param>
        protected override void Serialize(NetworkBinaryWriter writer)
        {
            // Start game (request) message structure:
            // int: total number of players in the game, N
            // int: total number of arenas in the game, M
            // word: length of serialised data of all players, L
            // L bytes: serialised data of N players (content known only by Player)
            // 32 * M bytes: names of M arenas
            byte[] writeBytes = StreamedData;
            writer.Write((int)PlayerCount);
            writer.Write((int)ArenaPlaylist.Count);
            writer.Write(checked((ushort)writeBytes.Length));
            writer.Write(writeBytes, 0, writeBytes.Length);
            foreach (string name in ArenaPlaylist)
                writer.Write((string)name, 32, true);
        }

        /// <summary>
        /// Reads the body of the message from serialised form.
        /// </summary>
        /// <param name="reader">Reader of serialised data.</param>
        protected override void Deserialize(NetworkBinaryReader reader)
        {
            PlayerCount = reader.ReadInt32();
            int arenaCount = reader.ReadInt32();
            int byteCount = reader.ReadUInt16();
            StreamedData = reader.ReadBytes(byteCount);
            ArenaPlaylist.Clear();
            for (int i = 0; i < arenaCount; ++i)
                ArenaPlaylist.Add(reader.ReadString(32));
        }

        /// <summary>
        /// Returns a String that represents the current Object. 
        /// </summary>
        public override string ToString()
        {
            return base.ToString() + " " + PlayerCount + " players";
        }
    }
}
