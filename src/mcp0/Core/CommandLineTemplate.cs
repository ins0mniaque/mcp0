using System.Diagnostics;

namespace mcp0.Core;

internal sealed class CommandLineTemplate(string template)
{
    private readonly string[] commandLineTemplate = CommandLine.Split(template);

    public ProcessStartInfo Render<T>(IReadOnlyDictionary<string, T> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        var commandLine = new string[commandLineTemplate.Length];
        for (var index = 0; index < commandLineTemplate.Length; index++)
            commandLine[index] = Template.Render(commandLineTemplate[index], arguments);

        CommandLine.Split(commandLine, out var command, out var argumentList, startInfo.Environment!);
        if (command is null)
            throw new FormatException($"Invalid command line: {string.Join(' ', commandLine)}");

        startInfo.FileName = command;
        foreach (var argument in argumentList ?? [])
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }
}