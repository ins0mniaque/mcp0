using System.CommandLine;
using System.CommandLine.Parsing;

namespace mcp0.Commands;

internal sealed class ShellCommand : Command
{
    public ShellCommand() : base("shell", "Run an interactive shell on the MCP server for one or more configured contexts")
    {
        var pathsArgument = new Argument<string[]>("files", "A list of context configuration files to run at start")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        AddAlias("sh");
        AddArgument(pathsArgument);

        this.SetHandler(Execute, pathsArgument);
    }

    private static Task Execute(string[] paths) => Execute(paths, CancellationToken.None);

    private static async Task Execute(string[] paths, CancellationToken cancellationToken)
    {
        var history = new List<string>();
        var hist = (int index) => index < 0 || index >= history.Count ? null : history[^(index + 1)];
        var hint = (string line) => line.Length is 0 ? "help" : null;

        while (!cancellationToken.IsCancellationRequested)
        {
            Terminal.Write("> ");

            var line = Terminal.ReadLine(hist, hint)?.Trim();
            if (line is null || line == "exit")
                break;

            var arguments = CommandLineStringSplitter.Instance.Split(line).ToArray();

            Terminal.WriteLine($"command not found: {arguments[0]}");
            if (history.Count is 0 || history[^1] != line)
                history.Add(line);

            await Task.CompletedTask;
        }
    }
}