using System.Diagnostics;

namespace mcp0.Core;

internal static class ToolCommand
{
    public static async Task<(string, string, int)> Run(string command, CancellationToken cancellationToken)
    {
        var arguments = command.Split(' ', 2);

        using var process = new Process();

        process.StartInfo.FileName = arguments[0];
        process.StartInfo.Arguments = arguments.Length is 2 ? arguments[1] : null;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;

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