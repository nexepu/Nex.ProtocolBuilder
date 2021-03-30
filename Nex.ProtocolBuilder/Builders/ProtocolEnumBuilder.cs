using System.Collections.Generic;
using System.Linq;
using Nex.ProtocolBuilder.Generator;
using Nex.ProtocolBuilder.Parsers;
using static System.Console;

namespace Nex.ProtocolBuilder.Builders
{
    namespace Nex.ProtocolBuilder.Builders
    {
        public class ProtocolEnumBuilder
        {
            public ProtocolEnumBuilder(IEnumerable<EnumParser> enumWriters)
            {
                EnumWriters = enumWriters.ToList();
            }

            public List<EnumParser> EnumWriters { get; }

            public void ParseFiles()
            {
                EnumWriters.ForEach(pc => pc.ParseFile());
            }

            public void GenerateFiles()
            {
                EnumWriters.ForEach(pc =>
                {
                    new EnumGenerator().WriteFile(pc.Class);
                    WriteLine($"> Parsing {pc.Class.Name} ...");
                });
            }
        }
    }
}