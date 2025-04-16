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
        var hints = new List<string>(proxy.Prompts.Count +
                                     proxy.Resources.Count +
                                     proxy.ResourceTemplates.Count +
                                     proxy.Tools.Count);

        hints.AddRange(proxy.Prompts.Select(static prompt => prompt.Name + '('));
        hints.AddRange(proxy.Resources.Select(static resource => resource.Uri));
        hints.AddRange(proxy.ResourceTemplates.Select(static resourceTemplate => resourceTemplate.UriTemplate));
        hints.AddRange(proxy.Tools.Select(static tool => tool.Name + '('));
        hints.Sort(StringComparer.Ordinal);

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

            return BinaryPrefixSearch(hints, line);
        }

        string? History(int index)
        {
            return index < 0 || index >= history.Count ? null : history[^(index + 1)];
        }
    }

    private static string? BinaryPrefixSearch(List<string> list, string prefix)
    {
        if (list.Count is 0)
            return null;

        var index = list.BinarySearch(prefix, StringComparer.Ordinal);
        index = index < 0 ? ~index : index + 1;
        if (index >= list.Count)
            return null;

        var element = list[index];
        if (!element.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        return element;
    }
}