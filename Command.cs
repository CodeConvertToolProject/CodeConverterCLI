using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeConverterCLI;

internal class Command(string name, string[] aliases, string description)
{
    public string Name { get; } = name;
    public string[] Aliases { get; } = aliases;
    public string Description { get; } = description;
    public delegate Task HandlerDelegate(params object?[] args);
    public HandlerDelegate? Handler { get; set; }
    public List<ICommandOption> Options { get; } = new List<ICommandOption>();
    public List<Command> Subcommands { get; } = new List<Command>();
    public Command? ParentCommand { get; set; }

    public void AddSubcommands(params Command[] subcommands)
    {
        foreach (var command in subcommands)
        {
            Subcommands.Add(command);
            command.ParentCommand = this;
        }
    }

    public string CommandPath()
    {
        var commands = new List<string>{Name};
        var parentCommand = ParentCommand;

        while (parentCommand != null)
        {
            parentCommand = parentCommand.ParentCommand;
            commands.Add(parentCommand!.Name);
        }

        commands.Reverse();

        return String.Join(' ', [.. commands]);
    }

    public void AddOptions(params ICommandOption[] options)
    {
        foreach (var option in options) Options.Add(option);
    }

    public async Task<int> Execute(string[] args)
    {
        if (args.Length == 0)
        {
            if (Handler != null) await Handler();
            return 0;
        }

        Command? matchingSubcommand = Subcommands.FirstOrDefault
            (subcommand => 
                subcommand!.Name.Equals(args[0]) || subcommand!.Aliases.Contains(args[0]),
                null
             );

        if (matchingSubcommand != null) return await matchingSubcommand.Execute(args.Skip(1).ToArray());

        var parser = new Parser(args);
        object?[] optionValues = [];

        try
        {
            optionValues = Options.Select(option => option.GetValue(parser)).ToArray();
        } catch (Exception ex)
        {
            DisplayHelp(ex.Message);
        }

        bool allRequiredExist = false;
        allRequiredExist = Options.All
            (option => 
                (option.GetValue(parser) != null && option.IsRequired) || !option.IsRequired
            );

        if (Handler != null && allRequiredExist)
        {
            await Handler(optionValues);
            return 0;
        }

        if (!allRequiredExist)
        {
            DisplayHelp("Incorrect Usage");
            return 1;
        }

        if (parser.GetArgumentByOption("--help") != null || parser.GetArgumentByOption("-h") != null)
        {
            DisplayHelp(String.Empty);
            return 0;
        }

        if (matchingSubcommand == null && optionValues.All(optionValue => optionValue == null)) 
        {
            DisplayHelp($"Unknown {(args[0].Contains('-')? "option" : "command")} {args[0]}");
            return 1;
        }
        
        return 0;
    }

    public virtual void DisplayHelp(string errorMessage)
    {
        string optionUsage = Options.Any(option => option.IsRequired) ? $"<option>" : $"[option]";
        string appName = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
        static string showAliases(string[] aliases_) => (aliases_.Length > 0 ? "|" + String.Join("|", aliases_) : "");

        if (!string.IsNullOrEmpty(errorMessage)) Console.WriteLine($"{appName}: {errorMessage}");
        
        Console.WriteLine($"Command: {Name}{showAliases(Aliases)}");
        Console.WriteLine($"Description: {Description}");
        Console.WriteLine($"Usage: {CommandPath} <command> | {optionUsage}");

        if (Subcommands.Count > 0)
        {
            Console.WriteLine("Commands:");
            foreach (var subcommand in Subcommands) 
            {
                Console.WriteLine($"  {subcommand.Name}{showAliases(subcommand.Aliases)}\t{subcommand.Description}");
            }
        }

        if (Options.Count > 0)
        {
            Console.WriteLine("Options:");
            foreach (var option in Options)
            {
                Console.WriteLine
                (
                    $"  --{String.Join('|', option.Names)}\t{(option.IsRequired ? "[REQUIRED]" : "")}{option.Description}"
                );
            }
            Console.WriteLine(" --help|-h\tDisplay this help text");
        }
    }
}
