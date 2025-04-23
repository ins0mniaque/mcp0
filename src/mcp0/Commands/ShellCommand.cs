using System.CommandLine.Invocation;
using System.Text.Json;

using mcp0.Core;
using mcp0.Mcp;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Types;

namespace mcp0.Commands;

internal sealed class ShellCommand : ProxyCommand
{
    private readonly List<string> history = new();

    public ShellCommand() : base("shell", "Run an interactive shell on the MCP server built from one or more configuration files")
    {
        AddAlias("sh");

        Sampling = new EmulatedSamplingCapability();
    }

    private bool reload;

    protected override async Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        Terminal.WriteLine(Root.Banner);

        do
        {
            reload = false;
            Terminal.Cursor.Hide();
            Terminal.Write("Connecting...\r");

            try
            {
                await ConnectAndRun(context, LogLevel.Warning, cancellationToken);
            }
            catch (Exception exception) when (exception is McpException or IOException or JsonException or FormatException or InvalidOperationException)
            {
                HandleException(exception);
                Reload(context, []);
            }
        }
        while (reload);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        Terminal.WriteLine($"Connected to {proxy.Clients.Count} {(proxy.Clients.Count is 1 ? "server" : "servers")}");
        Terminal.Cursor.Show();

        var hints = BuildHints(proxy);

        while (!cancellationToken.IsCancellationRequested)
        {
            Terminal.Write("> ");

            var line = Terminal.ReadLine(History, Hint).Trim();
            if (line is "q" or "quit" or "exit")
                break;

            try
            {
                if (FunctionCall.IsFunctionCallString(line))
                {
                    FunctionCall.Parse(line, out var function, out var arguments);

                    await Inspector.Call(proxy, function, arguments, cancellationToken);
                }
                else if (Uri.IsWellFormedUriString(line, UriKind.Absolute))
                    await Inspector.Read(proxy, line, cancellationToken);
                else
                    RunCommand(proxy, context, line);
            }
            catch (Exception exception) when (exception is McpException or IOException or JsonException or FormatException or InvalidOperationException)
            {
                HandleException(exception);
            }

            if (history.Count is 0 || !string.Equals(history[^1], line, StringComparison.Ordinal))
                history.Add(line);

            if (reload)
                break;
        }

        string? Hint(string line) => line.Length is 0 ? "help" : BinaryPrefixSearch(hints, line);
        string? History(int index) => index < 0 || index >= history.Count ? null : history[^(index + 1)];
    }

    private void RunCommand(McpProxy proxy, InvocationContext context, string line)
    {
        CommandLine.Split(line, out var command, out var arguments);
        if (command is null)
            return;

        if (command is "i" or "inspect")
            Inspector.Inspect(proxy);
        else if (command is "l" or "load")
            Reload(context, arguments ?? []);
        else if (command is "?" or "help")
            Help();
        else
            Terminal.WriteLine($"command not found: {command}");
    }

    private void Reload(InvocationContext context, string[] arguments)
    {
        context.ParseResult = context.Parser.Parse([Name, .. arguments]);
        reload = true;
    }

    private static void Help()
    {
        Terminal.WriteLine("Commands", ConsoleColor.Magenta);
        Terminal.WriteLine("  ?, help                         show this message");
        Terminal.WriteLine("  i, inspect                      inspect the current server");
        Terminal.WriteLine("  l, load [<files>...] [options]  load server from configuration files/options");
        Terminal.WriteLine("  q, quit, exit                   exit the shell");
    }

    private static readonly string[] commandHints = ["help", "inspect", "load ", "quit", "exit"];

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

    private static string EmulateSampling(CreateMessageRequestParams? request)
    {
        var model = "model";
        if (request?.ModelPreferences?.Hints is { } hints && hints.Count is not 0)
            model = hints[0].Name ?? model;

        return $"[Emulated sampling from {model}]";
    }

    private static void HandleException(Exception exception)
    {
        Terminal.WriteLine(exception.Message, ConsoleColor.Red);
    }
}