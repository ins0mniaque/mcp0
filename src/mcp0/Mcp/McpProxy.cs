using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Mcp;

internal sealed partial class McpProxy : IAsyncDisposable
{
    private readonly McpProxyOptions? proxyOptions;
    private readonly ILoggerFactory? loggerFactory;

    private ListPromptsResult listPromptsResult = new();
    private ListResourcesResult listResourcesResult = new();
    private ListResourceTemplatesResult listResourceTemplatesResult = new();
    private ListToolsResult listToolsResult = new();

    public McpProxy(McpProxyOptions? proxyOptions = null, ILoggerFactory? loggerFactory = null)
    {
        Prompts = new("prompt", static prompt => prompt.Name, proxyOptions?.Maps?.Prompt);
        Resources = new("resource", static resource => resource.Uri, proxyOptions?.Maps?.Resource);
        ResourceTemplates = new("resource template", static resourceTemplate => resourceTemplate.UriTemplate, proxyOptions?.Maps?.ResourceTemplate);
        Tools = new("tool", static tool => tool.Name, proxyOptions?.Maps?.Tool);

        this.proxyOptions = proxyOptions;
        this.loggerFactory = loggerFactory;
    }

    public IMcpServer? Server { get; internal set; }
    public IReadOnlyList<IMcpClient> Clients { get; private set; } = [];
    public McpProxyRegistry<Prompt> Prompts { get; }
    public McpProxyRegistry<Resource> Resources { get; }
    public McpProxyUriTemplateRegistry<ResourceTemplate> ResourceTemplates { get; }
    public McpProxyRegistry<Tool> Tools { get; }

    public async Task ConnectAsync(IEnumerable<IClientTransport> clientTransports, CancellationToken cancellationToken = default)
    {
        Server = null;
        foreach (var client in Clients)
            await client.DisposeAsync();

        var clientOptions = GetClientOptions();

        Clients = await Task.WhenAll(clientTransports.Select(CreateClient));

        if (proxyOptions?.LoggingLevel is { } loggingLevel)
            await SetLoggingLevel(loggingLevel, cancellationToken);

        await Task.WhenAll(InitializePrompts(cancellationToken),
                           InitializeResources(cancellationToken),
                           InitializeTools(cancellationToken));

        Task<IMcpClient> CreateClient(IClientTransport clientTransport)
        {
            return McpClientFactory.CreateAsync(clientTransport, clientOptions, loggerFactory, cancellationToken);
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return ConnectAsync([], cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Server = null;
        foreach (var client in Clients)
            await client.DisposeAsync();
    }

    private async Task InitializePrompts(CancellationToken cancellationToken)
    {
        Prompts.Clear();

        await Prompts.Register(Clients, async client =>
        {
            var prompts = await client.SafeListPromptsAsync(cancellationToken);

            return prompts.Select(static prompt => prompt.ProtocolPrompt).ToList();
        });

        listPromptsResult = new() { Prompts = Prompts.ToList() };

        if (Server is { } server)
            await server.SendNotificationAsync(NotificationMethods.PromptListChangedNotification, cancellationToken);
    }

    private async Task InitializeResources(CancellationToken cancellationToken)
    {
        Resources.Clear();
        ResourceTemplates.Clear();

        await Task.WhenAll(Resources.Register(Clients, client => client.SafeListResourcesAsync(cancellationToken)),
                           ResourceTemplates.Register(Clients, client => client.SafeListResourceTemplatesAsync(cancellationToken)));

        listResourcesResult = new() { Resources = Resources.ToList() };
        listResourceTemplatesResult = new() { ResourceTemplates = ResourceTemplates.ToList() };

        if (Server is { } server)
            await server.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification, cancellationToken);
    }

    private async Task InitializeTools(CancellationToken cancellationToken)
    {
        Tools.Clear();

        await Tools.Register(Clients, async client =>
        {
            var tools = await client.SafeListToolsAsync(null, cancellationToken);

            return tools.Select(static tool => tool.ProtocolTool).ToList();
        });

        listToolsResult = new() { Tools = Tools.ToList() };

        if (Server is { } server)
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