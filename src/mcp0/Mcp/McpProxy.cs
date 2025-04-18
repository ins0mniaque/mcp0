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
    private readonly IServiceProvider? serviceProvider;

    private ListPromptsResult listPromptsResult = new();
    private ListResourcesResult listResourcesResult = new();
    private ListResourceTemplatesResult listResourceTemplatesResult = new();
    private ListToolsResult listToolsResult = new();

    private readonly UriTemplateMatcherCache uriTemplateMatchers = new();
    private readonly Dictionary<string, string> renamedPrompts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> renamedResources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> renamedResourceTemplates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> renamedTools = new(StringComparer.Ordinal);

    public McpProxy(McpProxyOptions? proxyOptions = null, ILoggerFactory? loggerFactory = null, IServiceProvider? serviceProvider = null)
    {
        Prompts = new("prompt", static prompt => prompt.Name);
        Resources = new("resource", static resource => resource.Uri);
        ResourceTemplates = new("resource template", static resourceTemplate => resourceTemplate.UriTemplate);
        Tools = new("tool", static tool => tool.Name);

        this.proxyOptions = proxyOptions;
        this.loggerFactory = loggerFactory;
        this.serviceProvider = serviceProvider;
    }

    public IMcpServer? Server { get; private set; }
    public IReadOnlyList<IMcpClient> Clients { get; private set; } = [];
    public McpProxyRegistry<Prompt> Prompts { get; }
    public McpProxyRegistry<Resource> Resources { get; }
    public McpProxyUriTemplateRegistry<ResourceTemplate> ResourceTemplates { get; }
    public McpProxyRegistry<Tool> Tools { get; }

    public async Task ConnectAsync(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        foreach (var client in Clients)
            await client.DisposeAsync();

        Clients = clients;

        if (proxyOptions?.LoggingLevel is { } loggingLevel)
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
        if (Server is not null)
            throw new InvalidOperationException("Server is already running");

        var serverOptions = GetServerOptions();
        var serverName = proxyOptions?.ServerInfo?.Name ??
                         serverOptions.ServerInfo?.Name ??
                         DefaultImplementation.Name;

        await using var transport = new StdioServerTransport(serverName, loggerFactory);
        await using var server = McpServerFactory.Create(transport, serverOptions, loggerFactory, serviceProvider);

        try
        {
            Server = server;

            await server.RunAsync(cancellationToken);
        }
        finally
        {
            Server = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Server is not null)
            await Server.DisposeAsync();

        foreach (var client in Clients)
            await client.DisposeAsync();
    }

    internal string Map(Prompt prompt) => renamedPrompts.GetValueOrDefault(prompt.Name, prompt.Name);
    internal string Map(Resource resource) => renamedResources.GetValueOrDefault(resource.Uri, resource.Uri);
    internal string Map(ResourceTemplate resourceTemplate, string uri) => MapResourceTemplate(resourceTemplate.UriTemplate, uri);
    internal string Map(Tool tool) => renamedTools.GetValueOrDefault(tool.Name, tool.Name);

    private string MapResourceTemplate(string uriTemplate, string uri)
    {
        if (!renamedResourceTemplates.TryGetValue(uriTemplate, out var renamedUriTemplate))
            return uri;

        var matcher = uriTemplateMatchers.GetMatcher(uriTemplate);
        var renamedMatcher = uriTemplateMatchers.GetMatcher(renamedUriTemplate);

        // TODO: Parse uri with uriTemplate and fill out renamedTemplate

        return uri;
    }

    private async Task InitializePrompts(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Prompts.Clear();
        renamedPrompts.Clear();

        await Prompts.Register(clients, async client =>
        {
            var prompts = await client.SafeListPromptsAsync(cancellationToken);

            return prompts.Select(static prompt => prompt.ProtocolPrompt)
                          .Select(Map(proxyOptions?.Maps?.Prompt, renamedPrompts, static prompt => prompt.Name))
                          .Where(static prompt => prompt is not null)!
                          .ToList<Prompt>();
        });

        listPromptsResult = new() { Prompts = Prompts.ToList() };

        if (Server is { } server)
            await server.SendNotificationAsync(NotificationMethods.PromptListChangedNotification, cancellationToken);
    }

    private async Task InitializeResources(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Resources.Clear();
        ResourceTemplates.Clear();
        renamedResources.Clear();
        renamedResourceTemplates.Clear();

        var registerResources = Resources.Register(clients, async client =>
        {
            var resources = await client.SafeListResourcesAsync(cancellationToken);

            return resources.Select(Map(proxyOptions?.Maps?.Resource, renamedResources, static resource => resource.Uri))
                            .Where(static resource => resource is not null)!
                            .ToList<Resource>();
        });

        var registerResourceTemplates = ResourceTemplates.Register(clients, async client =>
        {
            var resourceTemplates = await client.SafeListResourceTemplatesAsync(cancellationToken);

            return resourceTemplates.Select(Map(proxyOptions?.Maps?.ResourceTemplate, renamedResourceTemplates, static resource => resource.UriTemplate))
                                    .Where(static resource => resource is not null)!
                                    .ToList<ResourceTemplate>();
        });

        await Task.WhenAll(registerResources, registerResourceTemplates);

        listResourcesResult = new() { Resources = Resources.ToList() };
        listResourceTemplatesResult = new() { ResourceTemplates = ResourceTemplates.ToList() };

        if (Server is { } server)
            await server.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification, cancellationToken);
    }

    private async Task InitializeTools(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Tools.Clear();
        renamedTools.Clear();

        await Tools.Register(clients, async client =>
        {
            var tools = await client.SafeListToolsAsync(null, cancellationToken);

            return tools.Select(static tool => tool.ProtocolTool)
                        .Select(Map(proxyOptions?.Maps?.Tool, renamedTools, static tool => tool.Name))
                        .Where(static tool => tool is not null)!
                        .ToList<Tool>();
        });

        listToolsResult = new() { Tools = Tools.ToList() };

        if (Server is { } server)
            await server.SendNotificationAsync(NotificationMethods.ToolListChangedNotification, cancellationToken);
    }

    private static Func<T, T?> Map<T>(Func<T, T?>? map, Dictionary<string, string> inverse, Func<T, string> keySelector) where T : class
    {
        if (map is null)
            return static item => item;

        return item =>
        {
            var key = keySelector(item);
            if (map(item) is not { } mappedItem)
                return null;

            var mappedKey = keySelector(mappedItem);
            if (!string.Equals(mappedKey, key, StringComparison.Ordinal))
                inverse[mappedKey] = key;

            return mappedItem;
        };
    }

    private async Task SetLoggingLevel(LoggingLevel level, CancellationToken cancellationToken)
    {
        var setLoggingLevelTasks = new List<Task>(Clients.Count);
        foreach (var client in Clients)
            setLoggingLevelTasks.Add(client.SafeSetLoggingLevel(level, cancellationToken));

        await Task.WhenAll(setLoggingLevelTasks);
    }
}