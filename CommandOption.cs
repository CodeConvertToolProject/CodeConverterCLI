using System;
using System.ComponentModel;
using System.Linq;

namespace CodeConverterCLI;

internal interface ICommandOption
{
    string[] Names { get; }
    public string Description { get; }
    public bool IsRequired { get; }
    public object? GetValue(Parser parser);
}

internal class CommandOption<T>(string[] names, string description, bool isRequired = false) : ICommandOption
{
    public string[] Names { get; } = names;
    public string Description { get; } = description;
    public bool IsRequired { get; } = isRequired;

    public T? GetValue(Parser parser)
    {
        string name = Names.FirstOrDefault(name => parser.GetArgumentByOption(name) != null, "");
        if (name.Equals("")) return default;

        try
        {
            return (T?)TypeDescriptor.GetConverter(typeof(T)).ConvertFrom
            (
                parser.GetArgumentByOption(name)!
            );
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    object? ICommandOption.GetValue(Parser parser)
    {
        return GetValue(parser);
    }
}
