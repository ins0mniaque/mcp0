using System.CommandLine;
using System.CommandLine.Parsing;

using mcp0.Core;
using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class ShellCommand : ProxyCommand
{
    public ShellCommand() : base("shell", "Run an interactive shell on the MCP server for one or more configured contexts")
    {
        var noReloadOption = new Option<bool>("--no-reload", "Do not reload when context configuration files change");
        var pathsArgument = new Argument<string[]>("files", "A list of context configuration files to run at start")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        AddAlias("sh");
        AddOption(noReloadOption);
        AddArgument(pathsArgument);

        this.SetHandler(Execute, pathsArgument, noReloadOption);
    }

    private Task Execute(string[] paths, bool noReload) => Execute(paths, noReload, CancellationToken.None);

    private async Task Execute(string[] paths, bool noReload, CancellationToken cancellationToken)
    {
        await ConnectAndRun(paths, noReload, LogLevel.Warning, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, CancellationToken cancellationToken)
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

            var commandLine = CommandLineStringSplitter.Instance.Split(line).ToArray();
            if (commandLine.Length is 0)
                continue;

            var command = commandLine[0];
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