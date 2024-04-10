using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeConverterCLI;

internal class RootCommand(string description): Command(
    Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location),
    [],
    description
    )
{
}
