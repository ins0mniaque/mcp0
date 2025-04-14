using System.CommandLine;
using System.CommandLine.Invocation;

using mcp0.Core;
using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class ShellCommand : ProxyCommand
{
    public ShellCommand() : base("shell", "Run an interactive shell on the MCP server for one or more configured contexts")
    {
        AddAlias("sh");
        AddOption(NoReloadOption);
        AddArgument(PathsArgument);
    }

    private Argument<string[]> PathsArgument { get; } = new("files", "A list of context configuration files to run at start")
    {
        Arity = ArgumentArity.ZeroOrMore
    };

    protected override async Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        var paths = context.ParseResult.GetValueForArgument(PathsArgument);

        await ConnectAndRun(context, paths, LogLevel.Warning, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        var history = new List<string>();
        var hist = (int index) => index < 0 || index >= history.Count ? null : history[^(index + 1)];
        var hint = (string line) => line.Length is 0 ? "help" : null;

        while (!cancellationToken.IsCancellationRequested)
        {
            Terminal.Write("> ");

            var line = Terminal.ReadLine(hist, hint).Trim();
            if (line is "exit")
                break;

            CommandLine.Split(line, out var command, out _);
            if (command is null)
                continue;

            if (command is "i" or "inspect")
                Inspector.Inspect(proxy);
            else
                Terminal.WriteLine($"command not found: {command}");

            if (history.Count is 0 || !string.Equals(history[^1], line, StringComparison.Ordinal))
                history.Add(line);
        }

        await Task.CompletedTask;
    }
}