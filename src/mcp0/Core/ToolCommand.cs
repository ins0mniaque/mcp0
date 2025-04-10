using System.CommandLine.Parsing;
using System.Diagnostics;

namespace mcp0.Core;

internal static class ToolCommand
{
    public static async Task<(string, string, int)> Run(string command, CancellationToken cancellationToken)
    {
        var arguments = CommandLineStringSplitter.Instance.Split(command).ToArray();
        var startInfo = new ProcessStartInfo
        {
            FileName = arguments[0],
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if(arguments.Length > 1)
            foreach (var argument in arguments[1..])
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