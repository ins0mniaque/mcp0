using System.CommandLine;
using System.Text.Json;

using mcp0.Core;
using mcp0.Models;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;

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
        var serverOptions = configuration.ToMcpServerOptions();

        await using var transport = serverOptions is null ? null : new ClientServerTransport(McpProxy.Name, loggerFactory);
        await using var serverTask = serverOptions is null ? null : new DisposableTask(async ct =>
        {
            // ReSharper disable once AccessToDisposedClosure
            await using var server = McpServerFactory.Create(transport!.ServerTransport, serverOptions);

            await server.RunAsync(ct);
        }, cancellationToken);

        var clientTransports = configuration.ToClientTransports();
        if (transport?.ClientTransport is { } clientTransport)
            clientTransports = clientTransports.Append(clientTransport).ToArray();

        proxyOptions.ServerInfo = McpProxy.CreateServerInfo(clientTransports);

        var proxy = new McpProxy(proxyOptions, loggerFactory);
        var clients = await clientTransports.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

        using var watchers = new CompositeDisposable<FileSystemWatcher>(noReload ? [] : paths.Select(CreateWatcher));
        foreach (var watcher in watchers)
        {
            // ReSharper disable once AccessToDisposedClosure
            watcher.Changed += async (_, _) => await Reload(proxy, paths, transport?.ClientTransport, loggerFactory, cancellationToken);
        }

        await proxy.InitializeAsync(clients, cancellationToken);
        await proxy.RunAsync(cancellationToken);
    }

    private static FileSystemWatcher CreateWatcher(string path) => new()
    {
        Path = Path.GetDirectoryName(path) ?? string.Empty,
        Filter = Path.GetFileName(path),
        NotifyFilter = NotifyFilters.LastWrite,
        EnableRaisingEvents = true
    };

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

            var clients = await clientTransports.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

            await proxy.InitializeAsync(clients, cancellationToken);

            logger.ConfigurationReloaded(paths);
        }
        catch (Exception exception) when (exception is IOException or JsonException or McpException)
        {
            logger.ConfigurationReloadFailed(exception, paths);
        }
    }
}