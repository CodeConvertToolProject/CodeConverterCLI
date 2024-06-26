﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeConverterCLI.CommandLib;

internal class RootCommand(string description, bool atLeastRequired = false): Command(
    Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "<exec_name>",
    [],
    description,
    atLeastRequired
    )
{
}
