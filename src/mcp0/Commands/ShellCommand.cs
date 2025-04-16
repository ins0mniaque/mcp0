using System.CommandLine;
using System.CommandLine.Invocation;

using mcp0.Core;
using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class ShellCommand : ProxyCommand
{
    public ShellCommand() : base("shell", "Run an interactive shell on the MCP server built from one or more configuration files")
    {
        AddAlias("sh");
        AddOption(NoReloadOption);
        AddArgument(PathsArgument);
    }

    private static new Argument<string[]> PathsArgument { get; } = new(ProxyCommand.PathsArgument.Name, ProxyCommand.PathsArgument.Description)
    {
        Arity = ArgumentArity.ZeroOrMore
    };

    protected override async Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        var paths = PathsArgument.GetValue(context);

        await ConnectAndRun(context, paths, LogLevel.Warning, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        var history = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            Terminal.Write("> ");

            var line = Terminal.ReadLine(History, Hint).Trim();
            if (line is "exit")
                break;

            CommandLine.Split(line, out var command, out _);
            if (command is null)
                continue;

            if (FunctionCall.TryParse(line, out var function, out var arguments))
                await Inspector.Call(proxy, function, arguments, cancellationToken);
            else if (Uri.IsWellFormedUriString(line, UriKind.Absolute))
                await Inspector.Read(proxy, line, cancellationToken);
            else if (command is "i" or "inspect")
                Inspector.Inspect(proxy);
            else
                Terminal.WriteLine($"command not found: {command}");

            if (history.Count is 0 || !string.Equals(history[^1], line, StringComparison.Ordinal))
                history.Add(line);
        }

        string? Hint(string line)
        {
            if (line.Length is 0)
                return "help";

            var tool = proxy.Tools.FirstOrDefault(tool => tool.Name.StartsWith(line, StringComparison.OrdinalIgnoreCase));
            if (tool is not null)
                return tool.Name + "(";

            var prompt = proxy.Prompts.FirstOrDefault(prompt => prompt.Name.StartsWith(line, StringComparison.OrdinalIgnoreCase));
            if (prompt is not null)
                return prompt.Name + "(";

            var resource = proxy.Resources.FirstOrDefault(resource => resource.Uri.StartsWith(line, StringComparison.OrdinalIgnoreCase));
            if (resource is not null)
                return resource.Uri;

            return null;
        }

        string? History(int index)
        {
            return index < 0 || index >= history.Count ? null : history[^(index + 1)];
        }
    }
}