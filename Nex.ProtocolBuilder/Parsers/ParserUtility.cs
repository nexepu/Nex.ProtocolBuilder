using System.Collections.Generic;
using System.Linq;
using Nex.ProtocolBuilder.Dictionnary;
using Nex.ProtocolBuilder.Protocol;
using Nex.ProtocolBuilder.Protocol.Enums;

namespace Nex.ProtocolBuilder.Parsers
{
    public class ParserUtility
    {
        private static readonly string[] BlacklistedImports = {
            "com.ankamagames.jerakine.network",
            "com.ankamagames.jerakine.network.networkmessage",
            "com.ankamagames.jerakine.network.inetworkmessage",
            "com.ankamagames.jerakine.network.inetworktype",
            "com.ankamagames.jerakine.network.networkType",
            "__AS3__.vec.vector",
            "flash.utils.idataoutput",
            "flash.utils.bytearray",
            "flash.utils.idatainput",
            "com.ankamagames.dofus.network.protocoltypeManager",
            "com.ankamagames.jerakine.network.utils.booleanbyteWrapper"
        };

        private static readonly string[] ForbiddenVariableNames = {
            "base",
            "params",
            "object",
            "messageId",
            "typeId"
        };

        public static string GetIParent(string parent, string @interface)
        {
            if (@interface == "INetworkType" && parent != "implements")
                return parent;
            if (@interface == "INetworkType" && parent == "implements")
                return "NetworkType";
            return parent;
        }

        public static string GetNamespace(string @namespace, int removeCount = 0)
        {
            if (!@namespace.Contains("com.ankamagames.dofus.network"))
                return @namespace;
            @namespace = @namespace.Replace("com.ankamagames.dofus.network.", "");
            var nameSpaceSplited = @namespace.Split('.');
            @namespace = "";
            for (var i = 0; i < nameSpaceSplited.Length - removeCount; i++)
            {
                var str = nameSpaceSplited[i];
                @namespace += str.Substring(0, 1).ToUpper() + str.Remove(0, 1) + ".";
            }

            var result = string.IsNullOrEmpty(@namespace) ? @namespace : @namespace.Remove(@namespace.Length - 1, 1);
            if (result == "Enums")
                result = Program.EnumsNamespace;
            return result;
        }

        public static string[] GetImports(string[] imports)
        {
            var retVal = new List<string>();
            foreach (var import in imports)
            {
                if (import.ToLower().Contains("com.ankamagames.jerakine"))
                    continue;
                if (BlacklistedImports.Contains(import.ToLower()))
                    continue;

                retVal.Add(GetNamespace(import, 1));

            }
            retVal.Add(Program.IONamespace);
            return retVal.ToArray();
        }

        public static string GetName(string name)
        {
            if (name == "id")
                return "objectId";

            if (ForbiddenVariableNames.Contains(name))
                return "@" + name;

            return name;
        }

        public static ProtocolClassVariable[] SortVars(ProtocolClassVariable[] vars, string fileStr)
        {
            var varsDictionary = vars.ToDictionary(v => v.Name);
            var retVal = new List<ProtocolClassVariable>();
            var lines = (fileStr + (char) 10 + (char) 10 + (char) 10 + (char) 10).Split((char) 10);
            var boolCount = 0;

            for (var index = 0; index < lines.Length - 4; index++)
            {
                var linesM1 = "";
                if (index > 1)
                    linesM1 = lines[index - 1];
                var line = lines[index];
                var line2 = lines[index + 1];
                var line3 = lines[index + 2];
                var line4 = lines[index + 4];

                var m = RegularExpression.GetRegex(RegexEnum.ReadFlagMetode).Match(line);
                if (m.Success)
                {
                    var currentVar = varsDictionary[m.Groups["name"].Value];
                    currentVar.ObjectType = "bool";
                    currentVar.MethodType = ReadMethodType.BooleanByteWraper;
                    currentVar.ReadMethod = m.Groups["flag"].Value;
                    currentVar.WriteMethod = m.Groups["flag"].Value;
                    currentVar.Index = m.Groups["name"].Index;
                    retVal.Insert(boolCount, currentVar);
                    boolCount += 1;
                    continue;
                }
                m = RegularExpression.GetRegex(RegexEnum.ReadMethodPrimitive).Match(line);
                if (m.Success)
                    retVal.Add(varsDictionary[m.Groups["name"].Value]);
                m = RegularExpression.GetRegex(RegexEnum.ReadMethodObject).Match(line);
                if (m.Success)
                {
                    var m2 = RegularExpression.GetRegex(RegexEnum.ReadMethodObjectProtocolManager).Match(linesM1);
                    if (m2.Success)
                    {
                        var var = varsDictionary[m.Groups["name"].Value];
                        var.MethodType = ReadMethodType.ProtocolTypeManager;
                        retVal.Add(var);
                    }
                    else
                    {
                        retVal.Add(varsDictionary[m.Groups["name"].Value]);
                    }
                }
                m = RegularExpression.GetRegex(RegexEnum.ReadVectorMethodObject)
                    .Match(line + (char) 10 + line2 + (char) 10 + line3);
                if (m.Success)
                    retVal.Add(varsDictionary[m.Groups["name"].Value]);
                m = RegularExpression.GetRegex(RegexEnum.ReadVectorMethodProtocolManager)
                    .Match(line + (char) 10 + line2 + (char) 10 + line3 + (char) 10 + line4);
                if (m.Success)
                {
                    var var = varsDictionary[m.Groups["name"].Value];
                    var.ObjectType = m.Groups["type"].Value;
                    var.MethodType = ReadMethodType.ProtocolTypeManager;
                    retVal.Add(var);
                }
                m = RegularExpression.GetRegex(RegexEnum.ReadVectorMethodPrimitive).Match(line + (char) 10 + line2);
                if (m.Success)
                    retVal.Add(varsDictionary[m.Groups["name"].Value]);
            }

            var rv = new List<ProtocolClassVariable>();
            foreach (var newVar in retVal)
            {
                newVar.Name = GetName(newVar.Name);
                rv.Add(newVar);
            }
            return rv.ToArray();
        }
    }
}