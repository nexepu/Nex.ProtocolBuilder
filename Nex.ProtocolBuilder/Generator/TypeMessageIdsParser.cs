using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nex.ProtocolBuilder.Dictionnary;

namespace Nex.ProtocolBuilder.Generator
{
    public class TypeMessageIdsParser
    {
        public static Dictionary<string, int> MessageIdsIndex = new();
        public static Dictionary<string, int> TypeIdsIndex = new();
        public static void ParseIds(string path)
        {
            Console.WriteLine("Parsing real ids...");
            var messageReceiverPath = path + "MessageReceiver.as";
            if (!File.Exists(messageReceiverPath))
                Console.WriteLine("WARN: MessageReceiver.as is not found, unable to find real messages ids");
            else 
                ParseMessageIds(messageReceiverPath);

            var protocolTypeManagerPath = path + "ProtocolTypeManager.as";
            if (!File.Exists(protocolTypeManagerPath))
                Console.WriteLine("WARN: ProtocolTypeManager.as is not found, unable to find real types ids");
            else
                ParseTypeIds(protocolTypeManagerPath);
        }
        private static void ParseMessageIds(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var match = RegularExpression.GetRegex(RegexEnum.MessageId).Match(line);
                if (!match.Success) 
                    continue;

                var id = int.Parse(match.Groups["id"].Value);
                var messageName = match.Groups["name"].Value;

                //Console.WriteLine($"Found message [{messageName}] with id {id}");
                if (MessageIdsIndex.ContainsKey(messageName))
                {
                    if (MessageIdsIndex[messageName] != id)
                    {
                        Console.WriteLine(
                            $"WARN: message {messageName} has a duplicated id, old:{MessageIdsIndex[messageName]} new:{id}");
                        MessageIdsIndex[messageName] = id;
                    }
                }
                else
                    MessageIdsIndex.Add(messageName, id);
            }
        }
        private static void ParseTypeIds(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var match = RegularExpression.GetRegex(RegexEnum.TypeId).Match(line);
                if (!match.Success)
                    continue;

                var id = int.Parse(match.Groups["id"].Value);
                var typeName = match.Groups["name"].Value;

                //Console.WriteLine($"Found type [{typeName}] with id {id}");
                if (TypeIdsIndex.ContainsKey(typeName))
                {
                    if (TypeIdsIndex[typeName] != id)
                    {
                        Console.WriteLine(
                            $"WARN: type {typeName} has a duplicated id, old:{TypeIdsIndex[typeName]} new:{id}");
                        TypeIdsIndex[typeName] = id;
                    }
                }
                else
                    TypeIdsIndex.Add(typeName, id);
            }
        }
    }
}