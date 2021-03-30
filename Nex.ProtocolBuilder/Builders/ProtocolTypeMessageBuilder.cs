using System.Collections.Generic;
using System.Linq;
using Nex.ProtocolBuilder.Generator;
using Nex.ProtocolBuilder.Parsers;
using static System.Console;

namespace Nex.ProtocolBuilder.Builders
{
    public class ProtocolTypeMessageBuilder
    {
        public ProtocolTypeMessageBuilder(IEnumerable<TypeMessageParser> classWriters)
        {
            ClassWriters = classWriters.ToList();
        }

        public static List<TypeMessageParser> ClassWriters { get; set; }

        public void ParseFiles()
        {
            ClassWriters.ForEach(pc => pc.ParseFile());
        }

        public void GenerateFiles(bool isType = false)
        {
            ClassWriters.ForEach(pc =>
            {
                TypeMessageGenerator.GenerateFile(pc.Class, isType);
                WriteLine($"> Parsing {pc.Class.Name} ...");
            });
        }
    }
}