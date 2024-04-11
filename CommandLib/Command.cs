using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeConverterCLI.CommandLib;

internal class Command(string name, string[] aliases, string description, bool atLeastRequired = false)
{
    public string Name { get; } = name;
    public string[] Aliases { get; } = aliases;
    public string Description { get; } = description;
    public bool AtLeastRequired { get; } = atLeastRequired;

    public delegate Task HandlerDelegateAsync(Dictionary<string, object?>? optionArguments);
    public delegate void HandlerDelegateSync(Dictionary<string, object?>? optionArguments);
    private HandlerDelegateAsync? HandlerAsync;
    private HandlerDelegateSync? HandlerSync;
    public List<ICommandOption> Options { get; } = [
        new CommandOption<bool>(["--help", "-h"], 
            "Display this help text")];

    public List<Command> Subcommands { get; } = [];
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
            commands.Add(parentCommand!.Name);
            parentCommand = parentCommand.ParentCommand;
        }

        commands.Reverse();

        return String.Join(' ', [..commands]);
    }

    public void AddOptions(params ICommandOption[] options)
    {
        foreach (var option in options) Options.Add(option);
    }

    public void SetHandler(HandlerDelegateAsync handler)
    {
        HandlerAsync = handler;
    }

    public void SetHandler(HandlerDelegateSync handler)
    {
        HandlerSync = handler;
    }

    private bool AllRequiredExist(Parser parser)
    {  
        return Options.All
            (option =>
                option.GetValue(parser) != null && option.IsRequired || !option.IsRequired
            );
    }

    private Dictionary<string, object?>? Parse(Parser parser)
    {
        try
        {
            return Options.Select(option =>
            {
                return (key: option.Names[0].Trim('-').ToLower(), value: option.GetValue(parser));
            }).ToDictionary();

        }
        catch (Exception ex)
        {
            DisplayHelp(ex.Message);
            return default;
        }
    }

    private int HandleUsageErrors(string arg)
    {
        if (Options.All(option => !option.Names.Contains(arg)))
        {
            DisplayHelp($"Unknown {(arg.Contains('-') ? "option" : "command")} {arg}");
            return 1;
        }

        return 0;
    }
    public async Task<int> Execute(string[] args)
    {
        if (args.Length == 0)
        {
            if (AtLeastRequired)
            {
                DisplayHelp("Incorrect usage");
                return 1;
            }

            if (HandlerAsync != null) await HandlerAsync(null);
            else if (HandlerSync != null) await Task.Run(() => HandlerSync(null));

            return 0;
        }

        Command? matchingSubcommand = Subcommands.FirstOrDefault
            (subcommand => 
                subcommand!.Name.Equals(args[0]) || subcommand!.Aliases.Contains(args[0]),
                null
             );

        if (matchingSubcommand != null) return await matchingSubcommand.Execute(args.Skip(1).ToArray());

        var parser = new Parser(args);
        Dictionary<string, object?>? optionArgs = Parse(parser);

        if (optionArgs == null) return 1;

        bool? help = (bool?)optionArgs.GetValueOrDefault("help", default);

        if (help != null && help == true)
        {
            DisplayHelp(String.Empty);
            return 0;
        }

        int handleUsageErrorsResult = HandleUsageErrors(args[0]);

        if (AllRequiredExist(parser) && handleUsageErrorsResult == 0)
        {
            if (HandlerAsync != null) await HandlerAsync(optionArgs);
            else if (HandlerSync != null) await Task.Run(() => HandlerSync(optionArgs));

            return 0;
        }

        return handleUsageErrorsResult;
    }

    public void DisplayHelp(string errorMessage)
    {
        string usage(string tag) => AtLeastRequired ? $"<{tag}>" : $"[{tag}]";

        string appName = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
        static string showAliases(string[] aliases_) => (aliases_.Length > 0 ? "|" + String.Join("|", aliases_) : "");

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{appName}: {errorMessage}\n");
            Console.ResetColor();
        }
        
        Console.WriteLine($"Command: {CommandPath()}{showAliases(Aliases)}");
        Console.WriteLine($"Description: {Description}");
        Console.WriteLine(
            $"Usage: {CommandPath()} {(Subcommands.Count > 0? usage("command") : "")}" +
            $"{(Subcommands.Count > 0 && Options.Count >  0? " | " : "")}" + 
            $"{(Options.Count > 0? usage("option"): "")}\n");

        if (Subcommands.Count > 0)
        {
            Console.WriteLine("Commands:");
            foreach (var subcommand in Subcommands) 
            {
                Console.WriteLine($"\t{subcommand.Name}{showAliases(subcommand.Aliases)}\t{subcommand.Description}");
            }
        }

        if (Options.Count > 0)
        {
            Console.WriteLine("Options:");
            foreach (var option in Options)
            {
                Console.WriteLine
                (
                    $"\t--{String.Join('|', option.Names)}\t{(option.IsRequired ? "[REQUIRED]" : "")}{option.Description}"
                );
            }
        }
    }
}
