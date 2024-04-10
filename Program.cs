using System;
using System.Threading.Tasks;

namespace CodeConverterCLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("A CLI tool for launguage conversion");
        rootCommand.AddOptions(
            new CommandOption<string>(
                new[] { "--version", "-v" },
                "Show version information"));



       return 0;
    }
}