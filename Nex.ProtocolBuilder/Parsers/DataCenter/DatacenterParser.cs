using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Nex.ProtocolBuilder.Parsers.DataCenter.Elements;

namespace Nex.ProtocolBuilder.Parsers.DataCenter
{
    [Serializable]
    public class DatacenterParser
    {
        public static readonly string PackagePattern = @"package ([\w\d\.]+)";
        public static readonly string ClassPatern = @"public class (\w+)";
        public static readonly string ClassPaternWithNumber = @"public class (\w+)#[0-9]+\s";
        public static readonly string ClassHeritagePattern = @"extends (?:[\w_]+\.)*(\w+)";

        public static readonly string ConstructorPattern =
            @"(?<acces>public|protected|private|internal)\s*function\s*(?<name>{0})\((?<argument>[^,)]+,?)*\)";

        public static readonly string ConstFieldPattern =
            @"(?<acces>public|protected|private|internal)\s*(?<static>static)?\s*const\s*(?<name>\w+):(?<type>[\w_\.]+(?:<(?:\w+\.)*(?<generictype>[\w_<>]+)>)?)(?<value>\s*=\s*.*)?;";

        public static readonly string FieldPattern =
            @"(?<acces>public|protected|private|internal)\s*(?<static>static)?\s*var\s*(?<name>[\w\d@]+):(?<type>[\w\d_\.<>]+)(?<value>\s*=\s*.*)?;";

        public static readonly string MethodPattern =
            @"((?<acces>public|protected|private|internal)|(?<override>override)\s)+\s*function\s*(?<prop>get|set)?\s+(?<name>\w+)\((?<argument>[^,)]+,?)*\)\s*:\s*(?:\w+\.)*(?<returntype>\w+)";


        private string m_fileText;
        private string[] m_fileLines;

        private Dictionary<int, int> m_brackets;

        public string Filename { get; private set; }
        public IEnumerable<KeyValuePair<string, string>> BeforeParsingRules { get; set; }

        public string[] IgnoredLines { get; set; }

        public ClassInfo Class { get; internal set; }

        public EnumInfo EnumInfo { get; internal set; }

        public List<MethodInfo> Constructors { get; internal set; }

        public List<FieldInfo> Fields { get; internal set; }

        public List<MethodInfo> Methods { get; internal set; }

        public List<PropertyInfo> Properties { get; internal set; }

        public bool IgnoreMethods { get; set; }

        public static bool HasHeritage(string heritage)
        {
            return !string.IsNullOrEmpty(heritage) && heritage != "Object";
        }

        public static string ToPascalCase(string name)
        {
            if (!char.IsLetter(name[0]))
                return char.ToUpper(name[1]) + name.Substring(2);

            return char.ToUpper(name[0]) + name.Substring(1);
        }

        public static SerializableDictionary<string, string> BeforeParsingReplacementRules =
            new()
            {
                {@"(var\s|this\.)base(?![\w\d])", @"$1@base"},
                // add '@' on variable name that are keyword in c#,
                {@"(var\s|this\.)object(?![\w\d])", @"$1@object"},
                {@"(var\s|this\.)operator(?![\w\d])", @"$1@operator"},
                {@"(var\s|this\.)params(?![\w\d])", @"$1@params"},
                // operator to @operator because it's a keyword

                {@"Vector\.([\w_\d]+) = new ([\w_\d]+)();", "$1 = new List<$2>();"},
                // convert "Vector." to List (C#) (and its props)
                {@"new Vector\.<([\d\w]+)>\((\d+), (true|false)\)", "new List<$1>($2)"},
                {@"new Vector\.<([\d\w]+)>", "new List<$1>()"},
                {@"(__AS3__\.vec\.)?Vector\.", "List"},
                {@"\.push\(", @".Add("},
                {@"\.length", @".Count"},
                {@"\bNumber", @"float"},
                // convert Number to float

                {@"static const", "const"},
                // manual fix
                {
                    @"const OPERATORS_LIST:Array\s?=\s?\[([^\]]+)\]",
                    "readonly static OPERATORS_LIST:Array=new string[]{$1}"
                },
                //another hack
                {@"(protected|private) var _rawZone", "public var rawZone"},
                {@"(protected|private) var _zoneSize = 4.29497e+009", "public var zoneSize"},
                {@"(protected|private) var _zoneShape = 4.29497e+009", "public var zoneShape"},
                {@"(protected|private) var _zoneMinSize = 4.29497e+009", "public var zoneMinSize"},
                {@"(protected|private) var _weight", "public var weight"},
                {@"(protected|private) var _type", "public var type"},
                {@"(protected|private) var _oldValue", "public var oldValue"},
                {@"(protected|private) var _newValue", "public var newValue"},
                {@"(protected|private) var _lang", "public var lang"},
                // ankama's devs are fucking idiots lol, they attempt to assign -1 to a uint field
                {@"public var iconId:uint", "public var iconId:int"},
            };

        public DatacenterParser(string filename)
        {
            Filename = filename;
            BeforeParsingRules = new Dictionary<string, string>();
            IgnoredLines = new string[0];
        }

        public DatacenterParser(string filename, IEnumerable<KeyValuePair<string, string>> beforeParsingRules,
            string[] ignoredLines)
        {
            Filename = filename;
            BeforeParsingRules = beforeParsingRules;
            IgnoredLines = ignoredLines;
        }

        public void ParseFile()
        {
            m_fileLines = File.ReadAllLines(Filename).Where(entry => !IsLineIgnored(entry))
                .Select(entry => ApplyRules(BeforeParsingRules, entry.Trim())).ToArray();
            m_fileText = string.Join("\r\n", m_fileLines);
            m_brackets = FindBracketsIndexesByLines(m_fileLines, '{', '}');
            if (Filename.Contains("#"))
            {
                Class = new ClassInfo
                {
                    Name = GetMatch(ClassPaternWithNumber),
                    Namespace = GetMatch(PackagePattern),
                    Heritage = GetMatch(ClassHeritagePattern),
                    AccessModifier = AccessModifiers.Public,
                    // we don't mind about this
                    ClassModifier = ClassInfo.ClassModifiers.None
                };
            }
            else
            {
                Class = new ClassInfo
                {
                    Name = GetMatch(ClassPatern),
                    Namespace = GetMatch(PackagePattern),
                    Heritage = GetMatch(ClassHeritagePattern),
                    AccessModifier = AccessModifiers.Public,
                    // we don't mind about this
                    ClassModifier = ClassInfo.ClassModifiers.None
                };
            }

            if (Class.Name == string.Empty)
            {
                throw new InvalidCodeFileException("This file does not contain a class");
            }

            Constructors = new List<MethodInfo>();

            ParseFields();
            Properties = new List<PropertyInfo>();
            //if (!IgnoreMethods)
            //{
            //  ParseConstructor();
            if (Class.Name != "FinishMoves")
            {
                if (Class.Name != "Spell")
                {
                    ParseMethods();
                }
            }

            //}
        }

        private void ParseConstructor()
        {
            var matchConstructor = Regex.Match(m_fileText,
                string.Format(
                    ConstructorPattern,
                    Class.Name), RegexOptions.Multiline);

            if (matchConstructor.Success)
            {
                var ctor = BuildMethodInfoFromMatch(matchConstructor, true);
                ctor.Statements = BuildMethodElementsFromMatch(matchConstructor).ToList();

                Constructors.Add(ctor);
            }
        }

        private void ParseFields()
        {
            Fields = new List<FieldInfo>();

            var matchConst = Regex.Match(m_fileText,
                ConstFieldPattern, RegexOptions.Multiline);
            while (matchConst.Success)
            {
                var field = new FieldInfo
                {
                    Modifiers =
                        (AccessModifiers)
                        Enum.Parse(typeof(AccessModifiers),
                            matchConst.Groups["acces"].Value,
                            true),
                    Name = matchConst.Groups["name"].Value,
                    Type =
                        matchConst.Groups["generictype"].Value == string.Empty
                            ? matchConst.Groups["type"].Value
                            : "List<" + matchConst.Groups["generictype"].Value + ">",
                    Value = matchConst.Groups["value"].Value.Replace("=", "").Trim(),
                    IsConst = true,
                    IsStatic = matchConst.Groups["static"].Value != string.Empty,
                };

                Fields.Add(field);

                matchConst = matchConst.NextMatch();
            }

            var matchVar = Regex.Match(m_fileText,
                FieldPattern, RegexOptions.Multiline);
            while (matchVar.Success)
            {
                var field = new FieldInfo
                {
                    Modifiers =
                        (AccessModifiers)
                        Enum.Parse(typeof(AccessModifiers), matchVar.Groups["acces"].Value,
                            true),
                    Name = matchVar.Groups["name"].Value,
                    Type = matchVar.Groups["type"].Value,
                    Value = matchVar.Groups["value"].Value.Replace("=", "").Trim(),
                    IsStatic = matchConst.Groups["static"].Value != string.Empty
                };
                if (field.Name == "type" && field.Type.Contains("Type") || field.Type.Contains("type"))
                {
                    goto pass;
                }

                Fields.Add(field);

                pass:
                matchVar = matchVar.NextMatch();
            }
        }

        private void ParseMethods()
        {
            Methods = new List<MethodInfo>();
            Properties = new List<PropertyInfo>();

            var matchMethods = Regex.Match(m_fileText,
                MethodPattern, RegexOptions.Multiline);

            while (matchMethods.Success)
            {

                var method = BuildMethodInfoFromMatch(matchMethods, false);
                method.Statements = BuildMethodElementsFromMatch(matchMethods).ToList();

                var propType = matchMethods.Groups["prop"].Value;
                if (!string.IsNullOrEmpty(propType))
                {
                    var prop = Properties.FirstOrDefault(x => x.Name == method.Name);

                    if (prop == null)
                    {
                        prop = new PropertyInfo()
                        {
                            Name = method.Name,
                            AccessModifier = method.AccessModifier,
                            PropertyType = method.ReturnType
                        };

                        Properties.Add(prop);
                    }

                    if (propType == "set")
                        prop.MethodSet = method;
                    else
                        prop.MethodGet = method;
                }
                else
                {
                    Methods.Add(method);
                }

                matchMethods = matchMethods.NextMatch();
                // do not support properties
                //if (!string.IsNullOrEmpty(matchMethods.Groups["prop"].Value))
                //{
                //    matchMethods = matchMethods.NextMatch();
                //    continue;
                //}

                //MethodInfo method = BuildMethodInfoFromMatch(matchMethods, false);
                //method.Statements = BuildMethodElementsFromMatch(matchMethods).ToList();

                //Methods.Add(method);

                //matchMethods = matchMethods.NextMatch();
            }
        }

        private MethodInfo BuildMethodInfoFromMatch(Match match, bool constructor)
        {
            var method = new MethodInfo
            {
                AccessModifier =
                    (AccessModifiers)
                    Enum.Parse(typeof(AccessModifiers),
                        match.Groups["acces"].Value, true),
                Name = match.Groups["name"].Value,
                Modifiers = match.Groups["override"].Value == "override"
                    ? new List<MethodInfo.MethodModifiers>(new[] {MethodInfo.MethodModifiers.Override})
                    : new List<MethodInfo.MethodModifiers>(new[] {MethodInfo.MethodModifiers.None}),
                ReturnType = constructor ? "" : match.Groups["returntype"].Value,
            };

            var args = new List<Argument>();
            foreach (var capture in match.Groups["argument"].Captures)
            {
                var arg = new Argument();

                var argStr = capture.ToString().Trim().Replace(",", "");

                arg.Name = argStr.Split(':').First().Trim();

                if (argStr.Contains("<"))
                {
                    var generictype = argStr.Split('<').Last().Split('>').First().Split('.').Last();

                    arg.Type = "List<" + generictype + ">";
                }
                else
                    arg.Type = argStr.Split(':').Last().Split('.').Last().Trim();

                if (arg.Type.Contains("="))
                {
                    arg.DefaultValue = arg.Type.Split('=').Last().Trim();
                    arg.Type = arg.Type.Split('=').First().Trim();
                }
                else if (!string.IsNullOrEmpty(args.LastOrDefault().DefaultValue))
                {
                    arg.DefaultValue = "null";
                }

                args.Add(arg);

            }

            method.Arguments = args.ToArray();

            if (!string.IsNullOrEmpty(match.Groups["prop"].Value))
            {
                PropertyInfo property;

                IEnumerable<PropertyInfo> propertiesExisting;
                if ((propertiesExisting = Properties.Where(entry => entry.Name == method.Name)).Any())
                {
                    property = propertiesExisting.First();
                }
                else
                {
                    property = new PropertyInfo
                    {
                        Name = method.Name,
                        AccessModifier = method.AccessModifier,
                    };
                }

                if (match.Groups["prop"].Value == "get")
                {
                    property.MethodGet = method;
                    property.PropertyType = method.ReturnType;
                }
                else if (match.Groups["prop"].Value == "set")
                {
                    property.MethodSet = method;
                }
            }

            return method;
        }

        private IEnumerable<IStatement> BuildMethodElementsFromMatch(Match match)
        {
            var bracketOpen =
                Array.FindIndex(m_fileLines, (entry) => entry.Contains(match.Groups[0].Value));
            if (!m_fileLines[bracketOpen].EndsWith("{"))
                bracketOpen++;
            var bracketClose = m_brackets[bracketOpen];

            var methodlines = new string[(bracketClose - 1) - bracketOpen];

            Array.Copy(m_fileLines, bracketOpen + 1, methodlines, 0, (bracketClose - 1) - bracketOpen);

            return ParseMethodExecutions(methodlines);
        }

        private static Dictionary<int, int> FindBracketsIndexesByLines(string[] lines, char startDelimter,
            char endDelemiter)
        {
            var elementsStack = new Stack<int>();
            var result = new Dictionary<int, int>();

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(startDelimter))
                    elementsStack.Push(i);

                if (lines[i].Contains(endDelemiter))
                {
                    var index = elementsStack.Pop();

                    result.Add(index, i);
                }
            }

            if (elementsStack.Count > 0)
                foreach (var i in elementsStack)
                {
                    throw new Exception($"Bracket '{startDelimter}' at index " + i + " is not closed");
                }

            return result;
        }

        private IEnumerable<IStatement> ParseMethodExecutions(IEnumerable<string> lines)
        {
            var result = new List<IStatement>();

            var controlsequenceDepth = 0;
            foreach (var line in lines.Select(entry => entry.Trim()))
            {
                if (IsLineIgnored(line))
                    continue;

                if (line == "{")
                    continue;

                if (line == "}")
                {
                    if (controlsequenceDepth > 0)
                    {
                        result.Add(new ControlStatementEnd());
                        controlsequenceDepth--;
                    }

                    continue;
                }

                IStatement statement;

                if (Regex.IsMatch(line, ControlStatement.Pattern))
                {
                    statement = ControlStatement.Parse(line);
                    controlsequenceDepth++;
                }

                else if (Regex.IsMatch(line, AssignationStatement.Pattern))
                {
                    statement = AssignationStatement.Parse(line);
                }

                //else if (Regex.IsMatch(line, InvokeExpression.Pattern))
                //{
                //    statement = InvokeExpression.Parse(line);
                //    if (!string.IsNullOrEmpty((statement as InvokeExpression).ReturnVariableAssignation) &&
                //        string.IsNullOrEmpty((statement as InvokeExpression).Preffix) &&
                //        Fields.Count(entry => entry.Name == ((InvokeExpression)statement).ReturnVariableAssignation) > 0)
                //    {
                //        (statement as InvokeExpression).Preffix = "(" +
                //                                                 Fields.Where(
                //                                                     entry =>
                //                                                     entry.Name ==
                //                                                     ((InvokeExpression)statement).
                //                                                         ReturnVariableAssignation).First().Type + ")"; // cast
                //    }

                //    // cast to generic type
                //    if (!string.IsNullOrEmpty((statement as InvokeExpression).Target) &&
                //        (statement as InvokeExpression).Name == "Add" &&
                //        Fields.Count(entry => entry.Name == ((InvokeExpression)statement).Target.Split('.').Last()) > 0)
                //    {
                //        string generictype =
                //            Fields.Where(entry => entry.Name == ((InvokeExpression)statement).Target.Split('.').Last()).
                //                First().Type.Split('<').Last().Split('>').First();

                //        (statement as InvokeExpression).Args[0] = "(" + generictype + ")" +
                //                                              (statement as InvokeExpression).Args[0];
                //    }
                //}

                else
                    statement = new UnknownStatement
                    {
                        Value = line
                    };

                result.Add(statement);
            }

            return result;
        }

        private string GetMatch(string pattern, int index = 1)
        {
            var matchedLine = m_fileLines
                .FirstOrDefault(entry => Regex.IsMatch(entry, pattern, RegexOptions.Multiline));

            if (matchedLine == null)
                return "";

            var match = Regex.Match(matchedLine, pattern, RegexOptions.Multiline);

            return match.Groups[index].Value;
        }

        private static string ApplyRules(IEnumerable<KeyValuePair<string, string>> rules, string str)
        {
            if (rules == null)
                return str;

            if (string.IsNullOrEmpty(str))
                return str;

            foreach (var rule in rules)
            {
                var replace = Regex.Replace(str, rule.Key, rule.Value);

                str = replace;
            }

            return str;
        }

        private bool IsLineIgnored(string line)
        {
            return IgnoredLines != null && IgnoredLines.Any(rule => Regex.IsMatch(line, rule));
        }
    }

    public class InvalidCodeFileException : Exception
    {
        public InvalidCodeFileException(string thisFileDoesNotContainAClass)
            : base(thisFileDoesNotContainAClass)
        {

        }
    }
}