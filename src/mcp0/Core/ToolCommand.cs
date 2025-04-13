using System.Diagnostics;

using ModelContextProtocol;

namespace mcp0.Core;

internal static class ToolCommand
{
    public static string[] Parse<T>(string command, IReadOnlyDictionary<string, T> arguments)
    {
        var commandLine = CommandLine.Split(command);
        for (var index = 0; index < commandLine.Length; index++)
            commandLine[index] = Template.Render(commandLine[index], arguments);

        return commandLine;
    }

    public static async Task<(string Stdout, string Stderr, int ExitCode)> Run(string[] commandLine, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        CommandLine.Split(commandLine, out var command, out var arguments, startInfo.Environment!);
        if (command is null)
            throw new McpException($"Invalid command line: {string.Join(' ', commandLine)}");

        startInfo.FileName = command;
        foreach (var argument in arguments ?? [])
            startInfo.ArgumentList.Add(argument);

        using var process = new Process();

        process.StartInfo = startInfo;
        process.Start();

        using var stdoutStream = new MemoryStream();
        using var stderrStream = new MemoryStream();

        var copyToStdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutStream, cancellationToken);
        var copyToStderrTask = process.StandardError.BaseStream.CopyToAsync(stderrStream, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        await copyToStdoutTask;
        await copyToStderrTask;

        stdoutStream.Position = 0;
        stderrStream.Position = 0;

        string stdout;
        using (var reader = new StreamReader(stdoutStream))
            stdout = await reader.ReadToEndAsync(cancellationToken);

        string stderr;
        using (var reader = new StreamReader(stderrStream))
            stderr = await reader.ReadToEndAsync(cancellationToken);

        return (stdout, stderr, process.ExitCode);
    }


}