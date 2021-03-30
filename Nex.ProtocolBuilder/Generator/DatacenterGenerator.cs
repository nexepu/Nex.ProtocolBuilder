using Nex.ProtocolBuilder.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nex.ProtocolBuilder.Parsers.DataCenter;
using Nex.ProtocolBuilder.Parsers.DataCenter.Elements;

namespace Nex.ProtocolBuilder.Generator
{
    public class DatacenterGenerator
    {
        public static void GenerateFile(DatacenterParser parser)
        {
            var writer = new StringBuilder();
            var classPath =
                $@"{Directory.GetCurrentDirectory()}\Output\DataCenter\{
                        parser.Class.Namespace.NamespaceToPathUncapitalized()
                    }\{parser.Class.Name}.cs";
            var path = $@"{Directory.GetCurrentDirectory()}\Output\DataCenter\{
                        parser.Class.Namespace.NamespaceToPathUncapitalized()}";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            if (!File.Exists(classPath))
                File.CreateText(classPath).Close();

            WriteUsingDirectives(writer);
            WriteNamespace(writer);
            WriteClass(parser, writer);
            if (string.IsNullOrEmpty(writer.ToString()) || !File.Exists(classPath)) return;
            File.WriteAllText(classPath, writer.ToString(), Encoding.UTF8);

        }
        private static void WriteUsingDirectives(StringBuilder Writer)
        {
            Writer.AppendLine("using System;");
            Writer.AppendLine("using System.Collections.Generic;");
            Writer.AppendLine("using " + Program.D2OClassesNamespace + ";");
            Writer.AppendLine("using " + Program.D2OClassesNamespace + ".Tools.D2o;");
        }
        private static void WriteNamespace(StringBuilder Writer)
        {
            Writer.AppendLine();
            Writer.AppendLine($"namespace " + Program.D2OClassesNamespace);
            Writer.AppendLine("{");
        }
        private static void WriteClass(DatacenterParser parser, StringBuilder Writer)
        {
            var module = parser.Fields.FirstOrDefault(x => x.Name == "MODULE");
            var idField = parser.Fields.FirstOrDefault(x => x.Name == "id");

            if (idField == null)
                idField = parser.Fields.FirstOrDefault(x => x.Modifiers == AccessModifiers.Public && x.Name.ToLower().Contains("id") && (x.Type == "int" || x.Type == "uint") && !x.Name.ToLower().Contains("type"));

            if (idField == null)
                idField = parser.Fields.FirstOrDefault(x => x.Modifiers == AccessModifiers.Public && x.Name.ToLower().Contains("id") && (x.Type == "int" || x.Type == "uint"));

            if (parser.Class.Name == "InfoMessage" || parser.Class.Name == "RideFood")
                idField = null;
            Writer.AppendLine("    [D2OClass(\"" + parser.Class.Name + "\", \"" + parser.Class.Namespace + "\")]");
            Writer.AppendLine("    [Serializable]");
            var s = (parser.Class.Heritage != "" && parser.Class.Heritage != "Object" && parser.Class.Heritage != "Proxy" ? " : " + parser.Class.Heritage : " : IDataObject" + (idField != null && idField.Type.ToString() != "String" ? ", IIndexedData" : ""));
            Writer.AppendLine($"    public class {parser.Class.Name}{s}");
            Writer.AppendLine("    {");
            foreach(var field in parser.Fields)
            {
                if (field.Modifiers != AccessModifiers.Public && field.Name != "MODULE")
                {
                    continue;
                }
                bool isI18nField = false;
                foreach (var property in parser.Properties)
                {
                    if (property.MethodGet != null)
                    {
                        var i18nAssignation = property.MethodGet.Statements.OfType<AssignationStatement>().
                            FirstOrDefault(x => x.Value.Contains("I18n.getText") && x.Value.Contains(field.Name));

                        if (i18nAssignation != null)
                        {
                            isI18nField = true;
                            break;
                        }
                    }
                }
                if (isI18nField)
                    Writer.AppendLine("        [I18NField]");
                WriteField(Writer, field);
            }
            if (idField != null && !DatacenterParser.HasHeritage(parser.Class.Heritage) && idField.Type.ToString() != "String")
            {
                Writer.AppendLine("        int IIndexedData.Id");
                Writer.AppendLine("        {");
                if (parser.Class.Name == "InfoMessage")
                    Writer.AppendLine("            get { return (int)(typeId * 10000 + messageId); }");
                else
                    Writer.AppendLine("            get { return (int)" + idField.Name + "; }");
                Writer.AppendLine("        }");
            }
            foreach (var field in parser.Fields)
            {
                if (field.Modifiers != AccessModifiers.Public || field.IsConst || field.IsStatic || field.Name == "MODULE")
                    continue;

                var name = DatacenterParser.ToPascalCase(field.Name);

                if (name == parser.Class.Name)
                    name += "_";

                Writer.AppendLine("        [D2OIgnore]");
                Writer.Append("        public ");
                Writer.Append(field.Type.ToString().Replace("float", "double"));
                Writer.AppendLine(" " + name);
                Writer.AppendLine("        {");
                Writer.AppendLine("            get { return this." + field.Name + "; }");
                Writer.AppendLine("            set { this." + field.Name + " = value; }");
                Writer.AppendLine("        }");
            }
            Writer.AppendLine("    }");
            Writer.AppendLine("}");
        }

        static void WriteField(StringBuilder Writer, FieldInfo field)
        {
            switch (field.Modifiers)
            {
                case AccessModifiers.Public:
                    Writer.Append("        public ");
                    break;
                case AccessModifiers.Protected:
                    Writer.Append("        protected ");
                    break;
                case AccessModifiers.Private:
                    Writer.Append("        private ");
                    break;
                case AccessModifiers.Internal:
                    Writer.Append("        internal ");
                    break;
            }

            if (field.IsConst)
            {
                Writer.Append("const ");
            }

            if (field.IsStatic)
            {
                Writer.Append("static ");
            }

            Writer.Append(field.Type.Replace("float", "double") + " ");
            Writer.Append(field.Name);

            if (!string.IsNullOrEmpty(field.Value))
            {
                Writer.Append(" = " + field.Value);
            }

            Writer.Append(";");
            Writer.AppendLine();
        }
    }
}
