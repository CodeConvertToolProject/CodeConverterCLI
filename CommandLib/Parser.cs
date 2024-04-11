using System.Collections.Generic;
using System.Linq;

namespace CodeConverterCLI.CommandLib;

internal class Parser
{
    private Dictionary<string, string> optionMap;

    public Parser(string[] arguments)
    {
        optionMap = [];
        ParseArguments(arguments);
    }

    private void ParseArguments(string[] arguments)
    {
        optionMap = arguments
            .Select((arg, index) => (arg, index))
            .Where(tuple => tuple.arg.StartsWith("--") || tuple.arg.StartsWith('-'))
            .Select(tuple =>
            {
                return 
                (
                    option: tuple.arg,
                    value: (tuple.index + 1 < arguments.Length && !arguments[tuple.index + 1].StartsWith('-')) ?
                        arguments[tuple.index + 1]: "true"
                );
            })
            .ToDictionary(tuple => tuple.option, tuple => tuple.value);
    }

    public string? GetArgumentByOption(string option)
    {
        return optionMap.GetValueOrDefault(option);
    }
}
