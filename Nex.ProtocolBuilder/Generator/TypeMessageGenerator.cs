using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nex.ProtocolBuilder.Extensions;
using Nex.ProtocolBuilder.Protocol;
using Nex.ProtocolBuilder.Protocol.Enums;

namespace Nex.ProtocolBuilder.Generator
{
    public class TypeMessageGenerator
    {
        public static void GenerateFile(ProtocolClass protocolClass, bool isType = false)
        {
            foreach (var vars in protocolClass.Variables)
            {
                if (vars.ReadMethod != null && vars.ReadMethod.Contains("Uh"))
                {
                    vars.ReadMethod = vars.ReadMethod.Replace("Uh", "U");
                }
                if (vars.WriteMethod != null && vars.WriteMethod.Contains("Uh"))
                {
                    vars.WriteMethod = vars.WriteMethod.Replace("Uh", "U");
                }
            }
            var writer = new StringBuilder();
            var classPath =
                $@"{Directory.GetCurrentDirectory()}/Output/{
                        protocolClass.Namespace.NamespaceToPath()
                    }/{protocolClass.Name}.cs";
            CreateRepositories(protocolClass);
            CreateFile(classPath);
            GenerateClass(writer, protocolClass, isType, classPath);
        }

        public static void CreateRepositories(ProtocolClass classParsed)
        {
            var path = $"Output/{classParsed.Namespace.NamespaceToPath()}";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static void CreateFile(string path)
        {
            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty);
        }

        private static void GenerateClass(StringBuilder writer, ProtocolClass classParsed, bool isType, string path)
        {
            WriteNamespace(isType, writer);
            WriteUsingDirectives(classParsed, writer);
            WriteClass(classParsed, writer);
            if (classParsed.Name == "RawDataMessage")
            {
                ParseRdm(classParsed, writer);
            }
            else
            {
                WriteProperties(classParsed, writer, isType);
                var hasParent = (classParsed.Parent != "NetworkType" && classParsed.Parent != "NetworkMessage" && string.IsNullOrEmpty(classParsed.Parent) == false);
                if (classParsed.Variables.Length >= 1 || hasParent == true)
                    WriteConstructor(classParsed, writer);

                writer.AppendLine();
                writer.AppendLine($"\t\tpublic {classParsed.Name}() {{ }}");
                WriteSerializeMethod(classParsed, writer);
                WriteDeserializeMethod(classParsed, writer);
            }

            WriteEndClass(writer);

            if (writer.Length == 0 || !File.Exists(path))
                return;
            File.WriteAllText(path, writer.ToString(), Encoding.UTF8);
        }

        private static void WriteNamespace(bool isType, StringBuilder writer)
        {
            writer.AppendLine(isType
                ? $"namespace " + Program.TypesNamespace
                : $"namespace " + Program.MessagesNamespace);

            writer.AppendLine("{");
        }

        private static void WriteUsingDirectives(ProtocolClass classParsed, StringBuilder writer)
        {
            foreach (var import in classParsed.Imports.Where(imp => imp != ""))
            {
                if (import.StartsWith("Types.")) 
                    continue;

                if (!import.StartsWith("Messages."))
                {
                    writer.AppendLine($"\tusing {import};");
                }
            }

            if(classParsed.Variables.Any(x => x.ObjectType == "Version"))
                writer.AppendLine($"\tusing Version = {Program.TypesNamespace}.Version;");

            writer.AppendLine("");
        }

        private static void WriteClass(ProtocolClass classParsed, StringBuilder writer)
        {
            var parent = classParsed.Parent;
            if (parent.Equals("NetworkMessage"))
            {
                parent = "Message";
            }
            if (parent.Equals("NetworkType"))
            {
                parent = "";
            }

            writer.AppendLine("\t[Serializable]");
            writer.AppendLine(string.IsNullOrEmpty(parent)
                ? $"\tpublic class {classParsed.Name}"
                : $"\tpublic class {classParsed.Name} : {parent}");
            writer.AppendLine("\t{");
        }

        private static void WriteProperties(ProtocolClass classParsed, StringBuilder writer, bool isType)
        {
            if (isType)
            {
                var id = TypeMessageIdsParser.TypeIdsIndex.ContainsKey(classParsed.Name)
                    ? TypeMessageIdsParser.TypeIdsIndex[classParsed.Name]
                    : classParsed.MessageId;

                if (classParsed.Parent == "NetworkType")
                {
                    writer.AppendLine($"\t\tpublic const short Id  = {id};");
                    writer.AppendLine("\t\tpublic virtual short TypeId => Id;");
                }
                else
                {
                    writer.AppendLine($"\t\tpublic new const short Id = {id};");
                    writer.AppendLine("\t\tpublic override short TypeId => Id;");
                }
            }
            else
            {
                var id = TypeMessageIdsParser.MessageIdsIndex.ContainsKey(classParsed.Name)
                    ? TypeMessageIdsParser.MessageIdsIndex[classParsed.Name]
                    : classParsed.MessageId;

                if (classParsed.Parent == "NetworkMessage")
                {
                    writer.AppendLine($"\t\tpublic const uint Id = {id};");
                    writer.AppendLine("\t\tpublic override uint MessageId => Id;");
                }
                else
                {
                    writer.AppendLine($"\t\tpublic new const uint Id = {id};");
                    writer.AppendLine("\t\tpublic override uint MessageId => Id;");
                }
            }

            foreach (var field in classParsed.Variables)
                switch (field.TypeOfVar)
                {
                    case VarType.Primitive:
                        writer.AppendLine(
                            $"\t\tpublic {field.ObjectType} {field.Name}" + " { get; set; }");
                        break;
                    case VarType.Object:
                        writer.AppendLine(
                            $"\t\tpublic {field.ObjectType} {field.Name}" + " { get; set; }");
                        break;
                    case VarType.Vector:
                        writer.AppendLine(
                            $"\t\tpublic IEnumerable<{field.ObjectType}> {field.Name}" + " { get; set; }");
                        break;
                }
        }

        private static void WriteConstructor(ProtocolClass classParsed, StringBuilder writer)
        {
            writer.AppendLine();

            var variables = classParsed.Variables;
            var parentVariables = new List<ProtocolClassVariable>();
            var fieldsToInit = new StringBuilder(); // fields in the void
            var initFields = new StringBuilder(); // set fields

            if (classParsed.Parent != "NetworkType" && classParsed.Parent != "NetworkMessage" && string.IsNullOrEmpty(classParsed.Parent) == false)
            {
                var parent = classParsed.Parent;
                var parents = new List<string>();
                while (true)
                {
                    var tmp = Builders.ProtocolTypeMessageBuilder.ClassWriters.FirstOrDefault(x => x.Class.Name.Equals(parent));
                    if (tmp != null)
                    {
                        parents.Add(tmp.Class.Name);
                        parent = tmp.Class.Parent;
                    }
                    else
                    {
                        break;
                    }
                }
                for (var index = (parents.Count - 1); index != -1; index--)
                {
                    var tmp2 = Builders.ProtocolTypeMessageBuilder.ClassWriters.FirstOrDefault(x => x.Class.Name.Equals(parents[index]));
                    if (tmp2 != null)
                        parentVariables.AddRange(tmp2.Class.Variables);
                }
                variables = parentVariables.Concat(classParsed.Variables).ToArray();
            }
            if (!variables.Any())
                return;

            foreach (var field in variables)
                switch (field.TypeOfVar)
                {
                    case VarType.Primitive:
                        fieldsToInit.Append($"{field.ObjectType} {field.Name.ToValidName()}, ");
                        initFields.AppendLine(
                            $"\t\t\tthis.{field.Name} = {field.Name.ToValidName()};");
                        break;
                    case VarType.Object:
                        fieldsToInit.Append($"{field.ObjectType} {field.Name.ToValidName()}, ");
                        initFields.AppendLine(
                            $"\t\t\tthis.{field.Name} = {field.Name.ToValidName()};");
                        break;
                    case VarType.Vector:
                        fieldsToInit.Append($"IEnumerable<{field.ObjectType}> {field.Name.ToValidName()}, ");
                        initFields.AppendLine(
                            $"\t\t\tthis.{field.Name} = {field.Name.ToValidName()};");
                        break;
                }
            if (fieldsToInit.Length > 0)
                fieldsToInit.Length -= 2;
            writer.AppendLine($"\t\tpublic {classParsed.Name}({fieldsToInit})");
            writer.AppendLine("\t\t{");
            writer.Append(initFields);
            writer.AppendLine("\t\t}");
        }

        private static void WriteSerializeMethod(ProtocolClass classParsed, StringBuilder writer)
        {
            writer.AppendLine();
            var initFields = new StringBuilder();
            if (classParsed.Parent != "NetworkType")
            {
                writer.AppendLine("\t\tpublic override void Serialize(IDataWriter writer)");
            }
            else
            {
                writer.AppendLine("\t\tpublic virtual void Serialize(IDataWriter writer)");
            }
            writer.AppendLine("\t\t{");

            if (classParsed.Parent != "NetworkMessage" && classParsed.Parent != "NetworkType")
                initFields.AppendLine("\t\t\tbase.Serialize(writer);");

            var flag = false;
            foreach (var field in classParsed.Variables)
                if (field.MethodType == ReadMethodType.BooleanByteWraper && !flag)
                {
                    flag = true;
                    initFields.AppendLine("\t\t\tvar flag = new byte();");
                }

            var flagCount = classParsed.Variables.Count(var => var.MethodType == ReadMethodType.BooleanByteWraper);
            var newflagCount = flagCount;
            foreach (var field in classParsed.Variables)
                switch (field.TypeOfVar)
                {
                    case VarType.Primitive:
                        switch (field.MethodType)
                        {
                            case ReadMethodType.Primitive:
                                initFields.AppendLine(
                                    $"\t\t\twriter.{field.WriteMethod}({field.Name});");
                                break;
                            case ReadMethodType.BooleanByteWraper:
                                initFields.AppendLine(
                                    $"\t\t\tflag = BooleanByteWrapper.SetFlag(flag, {Convert.ToUInt32(field.ReadMethod)}, {field.Name});");
                                break;
                        }
                        if (field.MethodType == ReadMethodType.BooleanByteWraper)
                        {
                            newflagCount -= 1;
                            if (flagCount == newflagCount + 8)
                            {
                                flagCount -= 8;
                                initFields.AppendLine("\t\t\twriter.WriteByte(flag);");
                            }
                            else if (newflagCount <= 0)
                            {
                                initFields.AppendLine("\t\t\twriter.WriteByte(flag);");
                            }
                        }
                        continue;
                    case VarType.Object:
                        switch (field.MethodType)
                        {
                            case ReadMethodType.ProtocolTypeManager:
                                initFields.AppendLine(
                                    $"\t\t\twriter.WriteShort({field.Name}.TypeId);");
                                break;
                            case ReadMethodType.SerializeOrDeserialize:
                                break;
                        }
                        initFields.AppendLine($"\t\t\t{field.Name}.Serialize(writer);");
                        break;
                    case VarType.Vector:
                        WriteSerializeVector(field, initFields);
                        break;
                }

            writer.Append(initFields);
            writer.AppendLine("\t\t}");
            writer.AppendLine();
        }

        private static void WriteSerializeVector(ProtocolClassVariable field, StringBuilder init)
        {
            init.AppendLine(!string.IsNullOrEmpty(field.VectorFieldWrite)
                ? $"\t\t\twriter.{field.VectorFieldWrite}({field.VectorFieldWrite.ToConverterCSharp()}{field.Name}.Count());"
                : $"\t\t\twriter.WriteShort((ushort){field.Name}.Count());");
            init.AppendLine(
                $"\t\t\tforeach (var objectToSend in {field.Name})");
            init.AppendLine("            {");
            switch (field.MethodType)
            {
                case ReadMethodType.ProtocolTypeManager:

                    init.AppendLine("\t\t\t\twriter.WriteShort(objectToSend.TypeId);");
                    init.AppendLine("\t\t\t\tobjectToSend.Serialize(writer);");
                    break;
                case ReadMethodType.SerializeOrDeserialize:
                    init.AppendLine("\t\t\t\tobjectToSend.Serialize(writer);");
                    break;
                case ReadMethodType.VectorPrimitive:
                    init.AppendLine(
                        $"\t\t\t\twriter.{field.WriteMethod}(objectToSend);");
                    break;
            }
            init.AppendLine("\t\t\t}");
        }


        private static void WriteDeserializeMethod(ProtocolClass classParsed, StringBuilder writer)
        {
            var initFields = new StringBuilder();
            if (classParsed.Parent != "NetworkType")
            {
                writer.AppendLine("\t\tpublic override void Deserialize(IDataReader reader)");
            }
            else
            {
                writer.AppendLine("\t\tpublic virtual void Deserialize(IDataReader reader)");
            }
            writer.AppendLine("\t\t{");

            if (classParsed.Parent != "NetworkMessage" && classParsed.Parent != "NetworkType")
                initFields.AppendLine("\t\t\tbase.Deserialize(reader);");

            var flag = false;
            foreach (var field in classParsed.Variables)
                if (field.MethodType == ReadMethodType.BooleanByteWraper && !flag)
                {
                    flag = true;
                    initFields.AppendLine("\t\t\tvar flag = reader.ReadByte();");
                }

            var flagCount = classParsed.Variables.Count(var => var.MethodType == ReadMethodType.BooleanByteWraper);
            var newflagCount = flagCount;
            foreach (var field in classParsed.Variables)

                switch (field.TypeOfVar)
                {
                    case VarType.Primitive:
                        switch (field.MethodType)
                        {
                            case ReadMethodType.Primitive:
                                initFields.AppendLine(
                                    $"\t\t\t{field.Name} = reader.{field.ReadMethod}();");
                                break;
                            case ReadMethodType.BooleanByteWraper:
                                initFields.AppendLine(
                                    $"\t\t\t{field.Name} = BooleanByteWrapper.GetFlag(flag, {Convert.ToInt32(field.ReadMethod)});");
                                break;
                        }
                        if (field.MethodType == ReadMethodType.BooleanByteWraper)
                        {
                            newflagCount -= 1;
                            if (flagCount == newflagCount + 8)
                            {
                                flagCount -= 8;
                                initFields.AppendLine("\t\t\tflag = reader.ReadByte();");
                            }
                        }
                        continue;
                    case VarType.Object:
                        switch (field.MethodType)
                        {
                            case ReadMethodType.ProtocolTypeManager:
                                initFields.AppendLine(
                                    $"\t\t\t{field.Name} = ProtocolTypeManager.GetInstance<{field.ObjectType}>(reader.ReadShort());");
                                break;
                            case ReadMethodType.SerializeOrDeserialize:
                                initFields.AppendLine(
                                    $"\t\t\t{field.Name} = new {field.ObjectType}();");
                                break;
                        }
                        initFields.AppendLine($"\t\t\t{field.Name}.Deserialize(reader);");
                        continue;
                    case VarType.Vector:
                        WriteDeserializeVector(field, initFields);
                        continue;
                }

            writer.Append(initFields);
            writer.AppendLine("\t\t}");
            writer.AppendLine();
        }

        private static void WriteDeserializeVector(ProtocolClassVariable field, StringBuilder init)
        {
            init.AppendLine(!string.IsNullOrEmpty(field.VectorFieldRead)
                ? $"\t\t\tvar {field.Name}Count = reader.{field.VectorFieldRead}();"
                : $"\t\t\tvar {field.Name}Count = reader.ReadShort();");
            init.AppendLine($"\t\t\tvar {field.Name}_ = new {field.ObjectType}[{field.Name}Count];");
            init.AppendLine($"\t\t\tfor (var {field.Name}Index = 0; {field.Name}Index < {field.Name}Count; {field.Name}Index++)");
            init.AppendLine("\t\t\t{");
            switch (field.MethodType)
            {
                case ReadMethodType.ProtocolTypeManager:
                    init.AppendLine($"\t\t\t\tvar objectToAdd = ProtocolTypeManager.GetInstance<{field.ObjectType}>(reader.ReadShort());");
                    init.AppendLine("\t\t\t\tobjectToAdd.Deserialize(reader);");
                    init.AppendLine($"\t\t\t\t{field.Name}_[{field.Name}Index] = objectToAdd;");
                    break;
                case ReadMethodType.SerializeOrDeserialize:
                    init.AppendLine($"\t\t\t\tvar objectToAdd = new {field.ObjectType}();");
                    init.AppendLine("\t\t\t\tobjectToAdd.Deserialize(reader);");
                    init.AppendLine($"\t\t\t\t{field.Name}_[{field.Name}Index] = objectToAdd;");
                    break;
                case ReadMethodType.VectorPrimitive:
                    init.AppendLine($"\t\t\t\t{field.Name}_[{field.Name}Index] = reader.{field.ReadMethod}();");
                    break;
            }
            init.AppendLine("\t\t\t}");
            init.AppendLine($"\t\t\t{field.Name} = {field.Name}_;");
        }

        private static void ParseRdm(ProtocolClass classParsed, StringBuilder writer)
        {
            var id = TypeMessageIdsParser.MessageIdsIndex.ContainsKey(classParsed.Name)
                ? TypeMessageIdsParser.MessageIdsIndex[classParsed.Name]
                : classParsed.MessageId;

            writer.AppendLine($"\t\tpublic const uint Id = {id};");
            writer.AppendLine("\t\tpublic override uint MessageId => Id;");
            writer.AppendLine("\t\tpublic byte[] Content { get; set; }");
            writer.AppendLine();
            writer.AppendLine($"\t\tpublic {classParsed.Name}() {{ }}");
            writer.AppendLine();
            writer.AppendLine($"\t\tpublic {classParsed.Name}(byte[] content)");
            writer.AppendLine("\t\t{");
            writer.AppendLine("\t\t\tContent = content;");
            writer.AppendLine("\t\t}");
            writer.AppendLine();
            writer.AppendLine("\t\tpublic override void Serialize(IDataWriter writer)");
            writer.AppendLine("\t\t{");
            writer.AppendLine("\t\t\tvar contentLength = Content.Length;");
            writer.AppendLine("\t\t\twriter.WriteVarInt(contentLength);");
            writer.AppendLine("\t\t\tfor (var i = 0; i < contentLength; i++)");
            writer.AppendLine("\t\t\twriter.WriteByte(Content[i]);");
            writer.AppendLine("\t\t}");
            writer.AppendLine("\t\tpublic override void Deserialize(IDataReader reader)");
            writer.AppendLine("\t\t{");
            writer.AppendLine("\t\t\tvar contentLength = reader.ReadVarInt();");
            writer.AppendLine("\t\t\treader.ReadBytes(contentLength);");
            writer.AppendLine("\t\t}");
        }

        private static void WriteEndClass(StringBuilder writer)
        {
            writer.AppendLine("\t}");
            writer.AppendLine("}");
        }
    }
}