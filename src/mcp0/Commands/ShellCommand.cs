using System.CommandLine.Invocation;

using mcp0.Core;
using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class ShellCommand : ProxyCommand
{
    private readonly List<string> history = new();

    public ShellCommand() : base("shell", "Run an interactive shell on the MCP server built from one or more configuration files")
    {
        AddAlias("sh");
    }

    private bool reload;

    protected override async Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        do
        {
            reload = false;

            await ConnectAndRun(context, LogLevel.Warning, cancellationToken);
        }
        while (reload);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        var hints = BuildHints(proxy);

        while (!cancellationToken.IsCancellationRequested)
        {
            Terminal.Write("> ");

            var line = Terminal.ReadLine(History, Hint).Trim();
            if (line is "exit")
                break;

            CommandLine.Split(line, out var command, out var arguments);
            if (command is null)
                continue;

            if (FunctionCall.TryParse(line, out var function, out var args))
                await Inspector.Call(proxy, function, args, cancellationToken);
            else if (Uri.IsWellFormedUriString(line, UriKind.Absolute))
                await Inspector.Read(proxy, line, cancellationToken);
            else if (command is "i" or "inspect")
                Inspector.Inspect(proxy);
            else if (command is "l" or "load")
                reload = Reload(context, arguments ?? []);
            else
                Terminal.WriteLine($"command not found: {command}");

            if (history.Count is 0 || !string.Equals(history[^1], line, StringComparison.Ordinal))
                history.Add(line);

            if (reload)
                break;
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

    private bool Reload(InvocationContext context, string[] arguments)
    {
        context.ParseResult = context.Parser.Parse([Name, .. arguments]);
        return true;
    }

    private static readonly string[] commandHints = ["help", "inspect", "load "];

    private static List<string> BuildHints(McpProxy proxy)
    {
        var hints = new List<string>(commandHints.Length +
                                     proxy.Prompts.Count +
                                     proxy.Resources.Count +
                                     proxy.ResourceTemplates.Count +
                                     proxy.Tools.Count);

        hints.AddRange(commandHints);
        hints.AddRange(proxy.Prompts.Select(static prompt => prompt.Name + '('));
        hints.AddRange(proxy.Resources.Select(static resource => resource.Uri));
        hints.AddRange(proxy.ResourceTemplates.Select(static resourceTemplate => resourceTemplate.UriTemplate));
        hints.AddRange(proxy.Tools.Select(static tool => tool.Name + '('));
        hints.Sort(StringComparer.Ordinal);

        return hints;
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