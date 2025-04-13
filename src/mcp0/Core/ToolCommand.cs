using System.CommandLine.Parsing;
using System.Diagnostics;

using ModelContextProtocol;

namespace mcp0.Core;

internal static class ToolCommand
{
    public static string[] Parse<T>(string command, IReadOnlyDictionary<string, T> arguments)
    {
        var commandLine = CommandLineStringSplitter.Instance.Split(command).ToArray();
        for (var index = 0; index < commandLine.Length; index++)
            commandLine[index] = Template.Render(commandLine[index], arguments);

        return commandLine;
    }

    public static async Task<(string, string, int)> Run(string[] commandLine, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        var commandIndex = ParseEnvironment(commandLine, startInfo.Environment);

        startInfo.FileName = commandLine[commandIndex];
        if (commandLine.Length > commandIndex + 1)
            foreach (var argument in commandLine[(commandIndex + 1)..])
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

    internal static int ParseEnvironment(string[] commandLine, IDictionary<string, string?> environment)
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
                throw new McpException($"Invalid environment variable name: {key}");

            environment[key.ToString()] = value.ToString();
        }

        return commandIndex;
    }
}