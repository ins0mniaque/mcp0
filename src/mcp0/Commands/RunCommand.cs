using System.CommandLine;
using System.Text.Json;

using mcp0.Core;
using mcp0.Models;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;

namespace mcp0.Commands;

internal sealed class RunCommand : Command
{
    public RunCommand() : base("run", "Run one or more configured contexts as an MCP server")
    {
        var noReloadOption = new Option<bool>("--no-reload", "Do not reload when context configuration files change");
        var pathsArgument = new Argument<string[]>("files", "A list of context configuration files to run")
        {
            Arity = ArgumentArity.OneOrMore
        };

        AddOption(noReloadOption);
        AddArgument(pathsArgument);

        this.SetHandler(Execute, pathsArgument, noReloadOption);
    }

    private static Task Execute(string[] paths, bool noReload) => Execute(paths, noReload, CancellationToken.None);

    private static async Task Execute(string[] paths, bool noReload, CancellationToken cancellationToken)
    {
        var proxyOptions = new McpProxyOptions
        {
            LoggingLevel = Log.Level?.ToLoggingLevel(),
            SetLoggingLevelCallback = static level => Log.Level = level.ToLogLevel()
        };

        Log.Level ??= LogLevel.Information;

        using var loggerFactory = Log.CreateLoggerFactory();

        var configuration = await Model.Load(paths, cancellationToken);
        var servers = configuration.ToClientTransports();

        proxyOptions.ServerInfo = McpProxy.CreateServerInfo(servers);

        var proxy = new McpProxy(proxyOptions, loggerFactory);
        var clients = await servers.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

        using var watchers = new CompositeDisposable<FileSystemWatcher>(noReload ? [] : paths.Select(CreateWatcher));
        foreach (var watcher in watchers)
        {
            // ReSharper disable once AccessToDisposedClosure
            watcher.Changed += async (_, _) => await Reload(proxy, paths, loggerFactory, cancellationToken);
        }

        await proxy.Initialize(clients, cancellationToken);
        await proxy.Run(cancellationToken);
    }

    private static FileSystemWatcher CreateWatcher(string path) => new()
    {
        Path = Path.GetDirectoryName(path) ?? string.Empty,
        Filter = Path.GetFileName(path),
        NotifyFilter = NotifyFilters.LastWrite,
        EnableRaisingEvents = true
    };

    private static async Task Reload(McpProxy proxy, string[] paths, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger<RunCommand>();

        try
        {
            logger.ConfigurationReloading(paths);

            var configuration = await Model.Load(paths, cancellationToken);
            var servers = configuration.ToClientTransports();
            var clients = await servers.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

            await proxy.Initialize(clients, cancellationToken);

            logger.ConfigurationReloaded(paths);
        }
        catch (Exception exception) when (exception is IOException or JsonException or McpException)
        {
            logger.ConfigurationReloadFailed(exception, paths);
        }
    }
}