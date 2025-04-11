using System.CommandLine;
using System.Text.Json;

using mcp0.Core;
using mcp0.Mcp;
using mcp0.Models;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;

namespace mcp0.Commands;

internal abstract class ProxyCommand(string name, string? description = null) : Command(name, description)
{
    protected abstract Task Run(McpProxy proxy, CancellationToken cancellationToken);

    protected async Task ConnectAndRun(string[] paths, bool noReload, LogLevel logLevel, CancellationToken cancellationToken)
    {
        var proxyOptions = new McpProxyOptions
        {
            LoggingLevel = Log.Level?.ToLoggingLevel(),
            SetLoggingLevelCallback = static level => Log.Level = level.ToLogLevel()
        };

        Log.Level ??= logLevel;

        using var loggerFactory = Log.CreateLoggerFactory();

        var configuration = await Model.Load(paths, cancellationToken);
        var serverOptions = configuration.ToMcpServerOptions();
        var serverName = proxyOptions.ServerInfo?.Name ??
                         serverOptions?.ServerInfo?.Name ??
                         ServerInfo.Default.Name;

        await using var transport = serverOptions is null ? null : new ClientServerTransport(serverName, loggerFactory);
        await using var serverTask = serverOptions is null ? null : new DisposableTask(async ct =>
        {
            // ReSharper disable once AccessToDisposedClosure
            await using var server = McpServerFactory.Create(transport!.ServerTransport, serverOptions);

            await server.RunAsync(ct);
        }, cancellationToken);

        var clientTransports = configuration.ToClientTransports();
        if (transport?.ClientTransport is { } clientTransport)
            clientTransports = clientTransports.Append(clientTransport).ToArray();

        proxyOptions.ServerInfo = ServerInfo.Create(clientTransports);

        await using var proxy = new McpProxy(proxyOptions, loggerFactory);

        using var watchers = new CompositeDisposable<FileSystemWatcher>(noReload ? [] : paths.Select(CreateWatcher));
        foreach (var watcher in watchers)
        {
            // ReSharper disable once AccessToDisposedClosure
            watcher.Changed += async (_, _) => await Reload(proxy, paths, transport?.ClientTransport, loggerFactory, cancellationToken);
        }

        var clients = await clientTransports.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

        await proxy.ConnectAsync(clients, cancellationToken);

        await Run(proxy, cancellationToken);
    }

    private static async Task Reload(McpProxy proxy, string[] paths, IClientTransport? clientTransport, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger<RunCommand>();

        try
        {
            logger.ConfigurationReloading(paths);

            var configuration = await Model.Load(paths, cancellationToken);

            var clientTransports = configuration.ToClientTransports();
            if (clientTransport is not null)
                clientTransports = clientTransports.Append(clientTransport).ToArray();

            await proxy.DisconnectAsync(cancellationToken);

            var clients = await clientTransports.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

            await proxy.ConnectAsync(clients, cancellationToken);

            logger.ConfigurationReloaded(paths);
        }
        catch (Exception exception) when (exception is IOException or JsonException or McpException)
        {
            logger.ConfigurationReloadFailed(exception, paths);
        }
    }

    private static FileSystemWatcher CreateWatcher(string path) => new()
    {
        Path = Path.GetDirectoryName(path) ?? string.Empty,
        Filter = Path.GetFileName(path),
        NotifyFilter = NotifyFilters.LastWrite,
        EnableRaisingEvents = true
    };
}