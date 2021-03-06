﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AW2.Helpers.Serialization;

namespace AW2.Net
{
    /// <summary>
    /// A network message between a management server and a game instance (client or server).
    /// </summary>
    public abstract class ManagementMessage : Message
    {
        private static Dictionary<string, Type> g_subclasses;

        protected virtual string[] Parameters { get { return new string[0]; } }

        private string OperationText
        {
            get
            {
                var attribute = (ManagementMessageAttribute)GetType().GetCustomAttributes(typeof(ManagementMessageAttribute), false).First();
                return "operation=" + attribute.Operation;
            }
        }

        private string ParameterText
        {
            get
            {
                var textBuilder = new StringBuilder();
                foreach (var param in Parameters)
                {
                    textBuilder.Append(";");
                    textBuilder.Append(param);
                }
                return textBuilder.ToString();
            }
        }

        private string Text { get { return OperationText + ParameterText; } }

        static ManagementMessage()
        {
            var subclassData =
                from type in Assembly.GetExecutingAssembly().GetTypes()
                where typeof(ManagementMessage).IsAssignableFrom(type) && !type.IsAbstract
                let attribute = (ManagementMessageAttribute)type.GetCustomAttributes(typeof(ManagementMessageAttribute), false).First()
                select new { attribute.Operation, type };
            g_subclasses = subclassData.ToDictionary(pair => pair.Operation, pair => pair.type);
        }

        protected ManagementMessage()
        {
        }

        protected static List<Dictionary<string, string>> Tokenize(string message)
        {
            return message.Split('\n')
                .Select(line => line.Split(';')
                    .Select(token => token.Split('='))
                    .Where(tokenSplit => tokenSplit.Length >= 2)
                    .ToDictionary(parts => parts[0], parts => parts[1]))
                .ToList();
        }

        protected static int? GetInt(Dictionary<string, string> line, string key)
        {
            try
            {
                if (line.ContainsKey(key)) return int.Parse(line[key]);
            }
            catch { }
            return null;
        }

        public new static ManagementMessage Deserialize(ArraySegment<byte> data, TimeSpan creationTime)
        {
            var text = Encoding.ASCII.GetString(data.Array, data.Offset, data.Count);
            var tokens = Tokenize(text);
            var operation = tokens[0]["operation"];
            var subclass = GetSubclass(operation);
            var message = (ManagementMessage)Activator.CreateInstance(subclass);
            message.Deserialize(tokens);
            message.CreationTime = creationTime;
            return message;
        }

        private static Type GetSubclass(string operation)
        {
            return g_subclasses[operation];
        }

        public override void Serialize(NetworkBinaryWriter writer)
        {
#if NETWORK_PROFILING
            using (new NetworkProfilingScope(this))
#endif
            {
                writer.Write(Encoding.ASCII.GetBytes(Text));
            }
        }

        protected abstract void Deserialize(List<Dictionary<string, string>> tokenizedLines);

        protected override void SerializeBody(NetworkBinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        protected override void Deserialize(NetworkBinaryReader reader)
        {
            throw new NotImplementedException();
        }
    }
}
