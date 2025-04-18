using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;

using mcp0.Core;
using mcp0.Mcp;
using mcp0.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Commands;

internal abstract class ProxyCommand : CancellableCommand
{
    protected ProxyCommand(string name, string? description = null) : base(name, description)
    {
        AddOption(ServerOption);
        AddOption(NoReloadOption);
        AddArgument(PathsArgument);
    }

    private static Option<string[]> ServerOption { get; } = new("--server", "Additional MCP server command or URI to add to the MCP server");
    private static Option<bool> NoReloadOption { get; } = new("--no-reload", "Do not reload when context configuration files change");

    private static Argument<string[]> PathsArgument { get; } = new("files", "The configuration files to build an MCP server from")
    {
        Arity = ArgumentArity.ZeroOrMore
    };

    protected abstract Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken);

    protected async Task ConnectAndRun(InvocationContext context, LogLevel logLevel, CancellationToken cancellationToken)
    {
        var paths = PathsArgument.GetValue(context);
        var servers = ServerOption.GetValue(context);
        var noReload = NoReloadOption.GetValue(context);

        var serviceProvider = context.GetServiceProvider();
        var configurationRoot = serviceProvider.GetService<IConfigurationRoot>();

        var proxyOptions = new McpProxyOptions
        {
            LoggingLevel = configurationRoot?.GetLogLevel()?.ToLoggingLevel(),
            SetLoggingLevelCallback = level => configurationRoot?.SetLogLevel(level.ToLogLevel())
        };

        configurationRoot?.TrySetLogLevel(logLevel);

        var configuration = await Configuration.Load(paths, cancellationToken);
        var clientTransports = configuration.ToClientTransports().ToList();

        foreach (var server in servers ?? [])
            clientTransports.Add(Server.FromString(server)?.ToClientTransport() ??
                                 throw new InvalidOperationException($"Invalid server: {server}"));

        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var serverOptions = configuration.ToMcpServerOptions(serviceProvider);
        var serverName = proxyOptions.ServerInfo?.Name ??
                         serverOptions?.ServerInfo?.Name ??
                         ServerInfo.Default.Name;

        await using var transport = serverOptions is null ? null : new ClientServerTransport(serverName, loggerFactory);
        await using var serverTask = serverOptions is null ? null : new DisposableTask(async ct =>
        {
            // ReSharper disable once AccessToDisposedClosure
            await using var server = McpServerFactory.Create(transport!.ServerTransport, serverOptions, loggerFactory, serviceProvider);

            await server.RunAsync(ct);
        }, cancellationToken);

        if (transport?.ClientTransport is { } clientTransport)
            clientTransports.Add(clientTransport);

        proxyOptions.ServerInfo = ServerInfo.Create(clientTransports);

        if (configuration.Patch?.Count > 0)
        {
            var patcher = new Patcher(configuration.Patch);

            proxyOptions.Maps = new()
            {
                Prompt = patcher.Patch,
                Resource = patcher.Patch,
                ResourceTemplate = patcher.Patch,
                Tool = patcher.Patch
            };
        }

        await using var proxy = new McpProxy(proxyOptions, loggerFactory, serviceProvider);

        using var watchers = new CompositeDisposable<FileSystemWatcher>(noReload ? [] : paths.Select(CreateWatcher));
        foreach (var watcher in watchers)
        {
            // ReSharper disable AccessToDisposedClosure
            watcher.Changed += async (_, _) => await Reload(proxy, paths, transport?.ClientTransport, loggerFactory, cancellationToken);
            // ReSharper restore AccessToDisposedClosure
        }

        var clients = await clientTransports.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

        await proxy.ConnectAsync(clients, cancellationToken);

        await Run(proxy, context, cancellationToken);
    }

    private static async Task Reload(McpProxy proxy, string[] paths, IClientTransport? clientTransport, ILoggerFactory? loggerFactory, CancellationToken cancellationToken)
    {
        var logger = loggerFactory?.CreateLogger<RunCommand>();

        try
        {
            logger?.ConfigurationReloading(paths);

            var configuration = await Configuration.Load(paths, cancellationToken);

            var clientTransports = configuration.ToClientTransports();
            if (clientTransport is not null)
                clientTransports = clientTransports.Append(clientTransport).ToArray();

            await proxy.DisconnectAsync(cancellationToken);

            var clients = await clientTransports.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

            await proxy.ConnectAsync(clients, cancellationToken);

            logger?.ConfigurationReloaded(paths);
        }
        catch (Exception exception) when (exception is IOException or JsonException or McpException)
        {
            logger?.ConfigurationReloadFailed(exception, paths);
        }
    }

    private static FileSystemWatcher CreateWatcher(string path) => new()
    {
        Path = Path.GetDirectoryName(Posix.ExpandPath(path)) ?? string.Empty,
        Filter = Path.GetFileName(path),
        NotifyFilter = NotifyFilters.LastWrite,
        EnableRaisingEvents = true
    };
}