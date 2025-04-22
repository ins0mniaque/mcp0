using System.CommandLine.Parsing;
using System.Diagnostics;

namespace mcp0.Core;

internal static class CommandLine
{
    public static string[] Split(string commandLine)
    {
        return CommandLineStringSplitter.Instance.Split(commandLine).ToArray();
    }

    public static void Split(string commandLine, out string? command, out string[]? arguments)
    {
        arguments = Split(commandLine);

        command = arguments.Length is 0 ? null : arguments[0];
        arguments = arguments.Length is 0 ? null : arguments[1..];
    }

    public static void Split(string commandLine, out string? command, out string[]? arguments, IDictionary<string, string> environment)
    {
        Split(Split(commandLine), out command, out arguments, environment);
    }

    public static void Split(string[] commandLine, out string? command, out string[]? arguments, IDictionary<string, string> environment)
    {
        var commandIndex = ParseEnvironment(commandLine, environment);

        command = commandLine.Length is 0 ? null : commandLine[commandIndex];
        arguments = commandLine.Length is 0 ? null : commandLine[(commandIndex + 1)..];
    }

    public static int ParseEnvironment(string[] commandLine, IDictionary<string, string> environment)
    {
        var commandIndex = -1;
        var keyValueRanges = (Span<Range>)stackalloc Range[2];

        while (++commandIndex < commandLine.Length)
        {
            var keyValue = commandLine[commandIndex].AsSpan();
            if (!DotEnv.Split(keyValue, keyValueRanges))
                break;

            var key = keyValue[keyValueRanges[0]];
            var value = keyValue[keyValueRanges[1]];
            if (!DotEnv.IsValidKey(key))
                break;

            environment[key.ToString()] = value.ToString();
        }

        return commandIndex;
    }

    public static async Task<(string Stdout, string Stderr, int ExitCode)> Run(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
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