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
        var noReloadOption = new Option<bool>("--no-reload", "Do not reload the context files when they change");
        var contextsArgument = new Argument<string[]>("contexts", "A list of context names and/or context files to run")
        {
            Arity = ArgumentArity.OneOrMore
        };

        AddOption(noReloadOption);
        AddArgument(contextsArgument);

        this.SetHandler(Execute, contextsArgument, noReloadOption);
    }

    private static Task Execute(string[] contexts, bool noReload) => Execute(contexts, noReload, CancellationToken.None);

    private static async Task Execute(string[] contexts, bool noReload, CancellationToken cancellationToken)
    {
        Log.Level ??= LogLevel.Information;

        using var loggerFactory = Log.CreateLoggerFactory();

        var config = await ContextConfig.Read(contexts, cancellationToken);
        var servers = config.ToMcpServerConfigs();
        var serverName = Server.NameFrom(servers.Select(static server => server.Name));
        var server = new Server(serverName, Server.Version, loggerFactory);
        var clients = await servers.CreateMcpClientsAsync(server.GetClientOptions(), loggerFactory, cancellationToken);

        using var watchers = new CompositeDisposable<FileSystemWatcher>(noReload ? [] : contexts.Select(CreateWatcher));
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
            var clients = await servers.CreateMcpClientsAsync(server.GetClientOptions(), loggerFactory, cancellationToken);

            await server.Initialize(clients, cancellationToken);

            logger.ContextReloaded(contexts);
        }
        catch (Exception exception) when (exception is IOException or JsonException or McpException)
        {
            logger.ContextReloadFailed(exception, contexts);
        }
    }
}