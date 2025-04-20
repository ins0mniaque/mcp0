using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Mcp;

internal sealed partial class McpProxy : IAsyncDisposable
{
    private readonly McpProxyOptions? proxyOptions;

    private ListPromptsResult listPromptsResult = new();
    private ListResourcesResult listResourcesResult = new();
    private ListResourceTemplatesResult listResourceTemplatesResult = new();
    private ListToolsResult listToolsResult = new();

    public McpProxy(McpProxyOptions? proxyOptions = null)
    {
        Prompts = new("prompt", static prompt => prompt.Name, proxyOptions?.Maps?.Prompt);
        Resources = new("resource", static resource => resource.Uri, proxyOptions?.Maps?.Resource);
        ResourceTemplates = new("resource template", static resourceTemplate => resourceTemplate.UriTemplate, proxyOptions?.Maps?.ResourceTemplate);
        Tools = new("tool", static tool => tool.Name, proxyOptions?.Maps?.Tool);

        this.proxyOptions = proxyOptions;
    }

    public IMcpServer? Server { get; private set; }
    public IReadOnlyList<IMcpClient> Clients { get; private set; } = [];
    public McpProxyRegistry<Prompt> Prompts { get; }
    public McpProxyRegistry<Resource> Resources { get; }
    public McpProxyUriTemplateRegistry<ResourceTemplate> ResourceTemplates { get; }
    public McpProxyRegistry<Tool> Tools { get; }

    public async Task ConnectAsync(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken = default)
    {
        foreach (var client in Clients)
            await client.DisposeAsync();

        Clients = clients;

        if (proxyOptions?.LoggingLevel is { } loggingLevel)
            await SetLoggingLevel(loggingLevel, cancellationToken);

        await Task.WhenAll(InitializePrompts(clients, cancellationToken),
                           InitializeResources(clients, cancellationToken),
                           InitializeTools(clients, cancellationToken));
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await ConnectAsync([], cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Server = null;
        foreach (var client in Clients)
            await client.DisposeAsync();
    }

    private async Task InitializePrompts(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Prompts.Clear();

        await Prompts.Register(clients, async client =>
        {
            var prompts = await client.SafeListPromptsAsync(cancellationToken);

            return prompts.Select(static prompt => prompt.ProtocolPrompt).ToList();
        });

        listPromptsResult = new() { Prompts = Prompts.ToList() };

        if (Server is { } server)
            await server.SendNotificationAsync(NotificationMethods.PromptListChangedNotification, cancellationToken);
    }

    private async Task InitializeResources(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Resources.Clear();
        ResourceTemplates.Clear();

        await Task.WhenAll(Resources.Register(clients, client => client.SafeListResourcesAsync(cancellationToken)),
                           ResourceTemplates.Register(clients, client => client.SafeListResourceTemplatesAsync(cancellationToken)));

        listResourcesResult = new() { Resources = Resources.ToList() };
        listResourceTemplatesResult = new() { ResourceTemplates = ResourceTemplates.ToList() };

        if (Server is { } server)
            await server.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification, cancellationToken);
    }

    private async Task InitializeTools(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Tools.Clear();

        await Tools.Register(clients, async client =>
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