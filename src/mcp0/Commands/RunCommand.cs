using System.CommandLine;
using System.Text.Json;

using mcp0.Configuration;
using mcp0.Core;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;

namespace mcp0.Commands;

internal sealed class RunCommand : Command
{
    public RunCommand() : base("run", "Run one or more contexts as an MCP server")
    {
        var contextsArgument = new Argument<string[]>("contexts", "A list of context names and/or context files to run")
        {
            Arity = ArgumentArity.OneOrMore
        };

        AddArgument(contextsArgument);

        this.SetHandler(Execute, contextsArgument);
    }

    private static Task Execute(string[] contexts) => Execute(contexts, CancellationToken.None);

    private static async Task Execute(string[] contexts, CancellationToken cancellationToken)
    {
        using var loggerFactory = Log.CreateLoggerFactory();

        var config = await ContextConfig.Read(contexts, cancellationToken);

        var servers = config.ToMcpServerConfigs();
        var clients = await servers.CreateMcpClientsAsync(loggerFactory, cancellationToken);

        var name = Server.NameFrom(servers.Select(static server => server.Name));
        var server = new Server(name, Server.Version, loggerFactory);

        using var watchers = new CompositeDisposable<FileSystemWatcher>(contexts.Select(CreateWatcher));
        foreach (var watcher in watchers)
        {
            // ReSharper disable once AccessToDisposedClosure
            watcher.Changed += async (_, _) => await Reload(server, contexts, loggerFactory, cancellationToken);
        }

        await server.Initialize(clients, cancellationToken);
        await server.Run(cancellationToken);
    }

    private static FileSystemWatcher CreateWatcher(string context) => new()
    {
        Path = Path.GetDirectoryName(context) ?? string.Empty,
        Filter = Path.GetFileName(context),
        NotifyFilter = NotifyFilters.LastWrite,
        EnableRaisingEvents = true
    };

    private static async Task Reload(Server server, string[] contexts, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger<RunCommand>();

        try
        {
            logger.ContextReloading(contexts);

            var config = await ContextConfig.Read(contexts, cancellationToken);

            var servers = config.ToMcpServerConfigs();
            var clients = await servers.CreateMcpClientsAsync(loggerFactory, cancellationToken);

            await server.Initialize(clients, cancellationToken);

            logger.ContextReloaded(contexts);
        }
        catch (Exception exception) when (exception is IOException or JsonException or McpException)
        {
            logger.ContextReloadFailed(exception, contexts);
        }
    }
}