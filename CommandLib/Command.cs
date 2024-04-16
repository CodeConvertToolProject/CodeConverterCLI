using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeConverterCLI.CommandLib;

public enum ErrorCode
{
    CMD_ERROR,
    APP_ERROR
}

public class CmdException(ErrorCode Error, string Message) : Exception
{
    public ErrorCode Error { get; private set; } = Error;
    new public string Message { get; private set; } = Message;
}

public class MyException : Exception
{
    public List<String> MyStrings { get; private set; }

    public MyException(List<String> myStrings)
    {
        this.MyStrings = myStrings;
    }
}

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

    private int HandleUsageErrors(string arg, bool allRequiredExist)
    {
        if (Options.All(option => !option.Names.Contains(arg)))
        {
            DisplayHelp($"Unknown {(arg.Contains('-') ? "option" : "command")} {arg}");
            return 1;
        }

        if (!allRequiredExist)
        {
            DisplayHelp("Specify all the required options");
            return 1;
        }
        return 0;
    }

    private static async Task InvokeAsyncHandler(HandlerDelegateAsync HandlerAsync, Dictionary<string, object?>? optionArguments)
    {
        try { 
            await HandlerAsync(optionArguments); 
        }
        catch (CmdException e) { 
            if (e.Error == ErrorCode.APP_ERROR) Console.WriteLine(e.Message); 
            else throw new Exception(e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private static async Task InvokeSyncHandler(HandlerDelegateSync HandlerSync, Dictionary<string, object?>? optionArguments)
    {
        try
        {
            await Task.Run(() => HandlerSync(optionArguments));
        }
        catch (CmdException e)
        {
            if (e.Error == ErrorCode.APP_ERROR) Console.WriteLine(e.Message);
            else throw new Exception(e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
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

            try
            {
                if (HandlerAsync != null) await InvokeAsyncHandler(HandlerAsync, null);
                else if (HandlerSync != null) await InvokeSyncHandler(HandlerSync, null);
            } 
            catch (Exception e)
            {
                DisplayHelp(e.Message);
                return 1;
            }

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

        bool allRequiredExist = AllRequiredExist(parser);
        int handleUsageErrorsResult = HandleUsageErrors(args[0], allRequiredExist);

        if (allRequiredExist && handleUsageErrorsResult == 0)
        {
            try
            {
                if (HandlerAsync != null) await InvokeAsyncHandler(HandlerAsync, optionArgs);
                else if (HandlerSync != null) await InvokeSyncHandler(HandlerSync, optionArgs);
            } catch (Exception e) 
            {
                DisplayHelp(e.Message);
                return 1;
            }

            return 0;
        }

        return handleUsageErrorsResult;
    }

    public void DisplayHelp(string errorMessage)
    {
        string usage(string tag) => AtLeastRequired ? $"<{tag}>" : $"[{tag}]";

        string appName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "<exec_name>";

        static string showAliases(string[] aliases_) => (aliases_.Length > 0 ? " | " + String.Join(" | ", aliases_) : "");

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{appName}: {errorMessage}\n");
            Console.ResetColor();
        }
        
        Console.WriteLine($"Command: {CommandPath()}{showAliases(Aliases)}");
        Console.WriteLine($"Description: {Description}\n");
        Console.WriteLine(
            $"Usage: {CommandPath()} {(Subcommands.Count > 0? usage("command") : "")}" +
            $"{(Subcommands.Count > 0 && Options.Count >  0? " | " : "")}" + 
            $"{(Options.Count > 0? usage("option"): "")}\n");

        if (Subcommands.Count > 0)
        {
            Console.WriteLine("Commands:");
            foreach (var subcommand in Subcommands) 
            {
                Console.WriteLine($"{subcommand.Name + showAliases(subcommand.Aliases), -20}{subcommand.Description}");
            }
            Console.WriteLine();
        }

        if (Options.Count > 0)
        {
            Console.WriteLine("Options:");
            foreach (var option in Options)
            {
                Console.WriteLine
                (
                    $"{String.Join(" | ", option.Names), -20}{(option.IsRequired ? "[REQUIRED] " : "")}{option.Description}"
                );
            }
        }
    }
}
