using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using mcp0.Core;
using mcp0.Mcp;
using mcp0.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Commands;

internal abstract partial class ProxyCommand : CancellableCommand
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

    protected SamplingCapability? DefaultSampling { get; init; }

    protected abstract Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken);

    [SuppressMessage("ReSharper", "AccessToDisposedClosure", Justification = "Closure is disposed before the captured variable is disposed")]
    protected async Task ConnectAndRun(InvocationContext context, LogLevel logLevel, CancellationToken cancellationToken)
    {
        var paths = PathsArgument.GetValue(context);
        var servers = ServerOption.GetValue(context);
        var noReload = NoReloadOption.GetValue(context);

        var serviceProvider = context.GetServiceProvider();
        var configurationRoot = serviceProvider.GetService<IConfigurationRoot>();
        var proxyOptions = new McpProxyOptions
        {
            Sampling = DefaultSampling,
            LoggingLevel = configurationRoot?.GetLogLevel()?.ToLoggingLevel(),
            SetLoggingLevelCallback = level => configurationRoot?.SetLogLevel(level.ToLogLevel())
        };

        configurationRoot?.TrySetLogLevel(logLevel);

        if (serviceProvider.GetService<Sampling>()?.ChatClient is { } chatClient)
            proxyOptions.Sampling = new() { SamplingHandler = chatClient.CreateSamplingWithModelHandler() };

        var configuration = await Configuration.Load(paths, cancellationToken);

        configuration.Merge(Configuration.Parse(servers));

        var clientTransports = configuration.ToClientTransports().ToList();
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var serverOptions = configuration.ToMcpServerOptions(serviceProvider);
        var serverInfo = proxyOptions.ServerInfo ??
                         serverOptions?.ServerInfo ??
                         ServerInfo.Default;

        await using var transport = serverOptions is null ? null : new ClientServerTransport(serverInfo.Name, loggerFactory);
        await using var serverTask = serverOptions is null ? null : new DisposableTask(async ct =>
        {
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

        await using var proxy = new McpProxy(proxyOptions, loggerFactory);

        using var watchers = new CompositeDisposable<FileSystemWatcher>(noReload ? [] : paths.Select(CreateWatcher));
        foreach (var watcher in watchers)
            watcher.Changed += async (_, _) => await Reload(proxy, paths, transport?.ClientTransport, loggerFactory, cancellationToken);

        await proxy.ConnectAsync(clientTransports, cancellationToken);

        await Run(proxy, context, cancellationToken);
    }

    private static async Task Reload(McpProxy proxy, string[] paths, IClientTransport? clientTransport, ILoggerFactory? loggerFactory, CancellationToken cancellationToken)
    {
        var logger = loggerFactory?.CreateLogger<ProxyCommand>() ?? (ILogger)NullLogger.Instance;

        try
        {
            LogConfigurationReloading(logger, paths);

            var configuration = await Configuration.Load(paths, cancellationToken);

            var clientTransports = configuration.ToClientTransports();
            if (clientTransport is not null)
                clientTransports = clientTransports.Append(clientTransport);

            await proxy.ConnectAsync(clientTransports, cancellationToken);

            LogConfigurationReloaded(logger, paths);
        }
        catch (Exception exception) when (exception is IOException or JsonException or McpException)
        {
            LogConfigurationReloadFailed(logger, exception, paths);
        }
    }

    private static FileSystemWatcher CreateWatcher(string path) => new()
    {
        Path = Path.GetDirectoryName(Posix.ExpandPath(path)) ?? string.Empty,
        Filter = Path.GetFileName(path),
        NotifyFilter = NotifyFilters.LastWrite,
        EnableRaisingEvents = true
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloading configuration: {Paths}")]
    private static partial void LogConfigurationReloading(ILogger logger, string[] paths);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloaded configuration: {Paths}")]
    private static partial void LogConfigurationReloaded(ILogger logger, string[] paths);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to reload configuration: {Paths}")]
    private static partial void LogConfigurationReloadFailed(ILogger logger, Exception exception, string[] paths);
}