using System;
using System.ComponentModel;
using System.Linq;

namespace CodeConverterCLI.CommandLib;

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
        string? nameFound = Names.FirstOrDefault(name => parser.GetArgumentByOption(name!) != null, default);
        if (nameFound == null) return default;

        try
        {
            return (T?)TypeDescriptor.GetConverter(typeof(T?)).ConvertFrom
            (
                parser.GetArgumentByOption(nameFound)!
            );
        }
        catch (Exception)
        {
            throw new Exception($"'{parser.GetArgumentByOption(nameFound)}' is not a valid value for '{nameFound}'");
        }
    }

    object? ICommandOption.GetValue(Parser parser)
    {
        return GetValue(parser);
    }
}
