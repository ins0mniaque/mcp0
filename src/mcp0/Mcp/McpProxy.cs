using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Mcp;

internal sealed partial class McpProxy : IServiceProvider, IAsyncDisposable
{
    private readonly McpProxyOptions proxyOptions;
    private readonly ILoggerFactory? loggerFactory;
    private readonly IServiceProvider? serviceProvider;
    private IMcpServer? runningServer;

    private ListPromptsResult listPromptsResult = new();
    private ListResourcesResult listResourcesResult = new();
    private ListResourceTemplatesResult listResourceTemplatesResult = new();
    private ListToolsResult listToolsResult = new();

    public McpProxy(McpProxyOptions? proxyOptions = null, ILoggerFactory? loggerFactory = null, IServiceProvider? serviceProvider = null)
    {
        Prompts = new("prompt", static prompt => prompt.Name);
        Resources = new("resource", static resource => resource.Uri);
        ResourceTemplates = new("resource template", static resourceTemplate => resourceTemplate.UriTemplate);
        Tools = new("tool", static tool => tool.Name);

        this.proxyOptions = proxyOptions ?? new();
        this.loggerFactory = loggerFactory;
        this.serviceProvider = serviceProvider;
    }

    public IReadOnlyList<IMcpClient> Clients { get; private set; } = [];
    public McpClientRegistry<McpClientPrompt> Prompts { get; }
    public McpClientRegistry<Resource> Resources { get; }
    public McpClientTemplateRegistry<ResourceTemplate> ResourceTemplates { get; }
    public McpClientRegistry<McpClientTool> Tools { get; }

    public LoggingLevel? LoggingLevel => runningServer?.LoggingLevel;
    public IServiceProvider? Services => runningServer?.Services ??
                                         serviceProvider ??
                                         (loggerFactory is null ? null : this);

    object? IServiceProvider.GetService(Type serviceType)
    {
        return serviceType == typeof(ILoggerFactory) ? loggerFactory : null;
    }

    public async Task ConnectAsync(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        foreach (var client in Clients)
            await client.DisposeAsync();

        Clients = clients;

        if (proxyOptions.LoggingLevel is { } loggingLevel)
            await SetLoggingLevel(loggingLevel, cancellationToken);

        await InitializePrompts(clients, cancellationToken);
        await InitializeResources(clients, cancellationToken);
        await InitializeTools(clients, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await ConnectAsync([], cancellationToken);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (runningServer is not null)
            throw new InvalidOperationException("Server is already running");

        var serverOptions = GetServerOptions();
        var serverName = proxyOptions.ServerInfo?.Name ??
                         serverOptions.ServerInfo?.Name ??
                         DefaultImplementation.Name;

        await using var transport = new StdioServerTransport(serverName, loggerFactory);
        await using var server = McpServerFactory.Create(transport, serverOptions, loggerFactory, serviceProvider);

        try
        {
            runningServer = server;

            await server.RunAsync(cancellationToken);
        }
        finally
        {
            runningServer = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (runningServer is not null)
            await runningServer.DisposeAsync();

        foreach (var client in Clients)
            await client.DisposeAsync();
    }

    private async Task InitializePrompts(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Prompts.Clear();

        await Prompts.Register(clients, client => client.SafeListPromptsAsync(cancellationToken));

        listPromptsResult = new()
        {
            Prompts = Prompts.Select(static prompt => prompt.ProtocolPrompt).ToList()
        };

        if (runningServer is { } server)
            await server.SendNotificationAsync(NotificationMethods.PromptListChangedNotification, cancellationToken);
    }

    private async Task InitializeResources(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Resources.Clear();
        ResourceTemplates.Clear();

        await Task.WhenAll(
            Resources.Register(clients, client => client.SafeListResourcesAsync(cancellationToken)),
            ResourceTemplates.Register(clients, client => client.SafeListResourceTemplatesAsync(cancellationToken)));

        listResourcesResult = new()
        {
            Resources = Resources.ToList()
        };

        listResourceTemplatesResult = new()
        {
            ResourceTemplates = ResourceTemplates.ToList()
        };

        if (runningServer is { } server)
            await server.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification, cancellationToken);
    }

    private async Task InitializeTools(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Tools.Clear();

        await Tools.Register(clients, client => client.SafeListToolsAsync(null, cancellationToken));

        listToolsResult = new()
        {
            Tools = Tools.Select(static tool => tool.ProtocolTool).ToList()
        };

        if (runningServer is { } server)
            await server.SendNotificationAsync(NotificationMethods.ToolListChangedNotification, cancellationToken);
    }

    private async Task SetLoggingLevel(LoggingLevel level, CancellationToken cancellationToken)
    {
        var setLoggingLevelTasks = new List<Task>(Clients.Count);
        foreach (var client in Clients)
            setLoggingLevelTasks.Add(client.SafeSetLoggingLevel(level, cancellationToken));

        await Task.WhenAll(setLoggingLevelTasks);
    }
}