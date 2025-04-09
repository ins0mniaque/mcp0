using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Core;

internal sealed partial class McpProxy
{
    public static string Name { get; } = typeof(McpProxy).Assembly.GetName().Name ?? "mcp0";
    public static string Version { get; } = typeof(McpProxy).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static Implementation CreateServerInfo(IEnumerable<IClientTransport> transports) => new()
    {
        Name = string.Join('/', transports.Select(static transport => transport.Name).DefaultIfEmpty(Name)),
        Version = Version
    };

    private readonly Dictionary<string, (IMcpClient Client, McpClientPrompt Prompt)> prompts;
    private readonly Dictionary<string, (IMcpClient Client, Resource Resource)> resources;
    private readonly Dictionary<string, (IMcpClient Client, ResourceTemplate ResourceTemplate)> resourceTemplates;
    private readonly Dictionary<string, (IMcpClient Client, McpClientTool Tool)> tools;
    private readonly ConcurrentDictionary<IMcpClient, byte> disabledCompletionClients = new();
    private readonly McpProxyOptions proxyOptions;
    private readonly ILoggerFactory loggerFactory;

    private IMcpServer? runningServer;
    private Task<ListPromptsResult> listPromptsResultTask = Task.FromResult(new ListPromptsResult());
    private Task<ListResourcesResult> listResourcesResultTask = Task.FromResult(new ListResourcesResult());
    private Task<ListResourceTemplatesResult> listResourceTemplatesResultTask = Task.FromResult(new ListResourceTemplatesResult());
    private Task<ListToolsResult> listToolsResultTask = Task.FromResult(new ListToolsResult());

    public McpProxy(McpProxyOptions proxyOptions, ILoggerFactory loggerFactory)
    {
        Prompts = prompts = new(StringComparer.Ordinal);
        Resources = resources = new(StringComparer.Ordinal);
        ResourceTemplates = resourceTemplates = new(StringComparer.Ordinal);
        Tools = tools = new(StringComparer.Ordinal);

        this.proxyOptions = proxyOptions;
        this.loggerFactory = loggerFactory;
    }

    public IReadOnlyList<IMcpClient> Clients { get; private set; } = [];
    public IReadOnlyDictionary<string, (IMcpClient Client, McpClientPrompt Prompt)> Prompts { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, Resource Resource)> Resources { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, ResourceTemplate ResourceTemplate)> ResourceTemplates { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, McpClientTool Tool)> Tools { get; }

    public async Task Initialize(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Clients = clients;
        disabledCompletionClients.Clear();

        if (proxyOptions.LoggingLevel is { } loggingLevel)
            await SetLoggingLevel(loggingLevel, cancellationToken);

        await InitializePrompts(clients, cancellationToken);
        await InitializeResources(clients, cancellationToken);
        await InitializeTools(clients, cancellationToken);
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        if (runningServer is not null)
            throw new InvalidOperationException("Server is already running");

        var serverOptions = GetServerOptions();

        await using var transport = new StdioServerTransport(proxyOptions.ServerInfo?.Name ?? Name, loggerFactory);
        await using var server = McpServerFactory.Create(transport, serverOptions, loggerFactory);

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

    private async Task InitializePrompts(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        prompts.Clear();

        var promptsTasks = new List<Task<IList<McpClientPrompt>>>(clients.Count);
        foreach (var client in clients)
            promptsTasks.Add(client.SafeListPromptsAsync(cancellationToken));

        await Register(prompts, promptsTasks, static prompt => prompt.Name);

        listPromptsResultTask = Task.FromResult(new ListPromptsResult
        {
            Prompts = prompts.Select(static entry => entry.Value.Prompt.ProtocolPrompt).ToList()
        });

        if (runningServer is { } server)
            await server.SendNotificationAsync(NotificationMethods.PromptListChangedNotification, cancellationToken);
    }

    private async Task InitializeResources(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        resources.Clear();
        resourceTemplates.Clear();

        var resourcesTasks = new List<Task<IList<Resource>>>(clients.Count);
        var resourceTemplatesTasks = new List<Task<IList<ResourceTemplate>>>(clients.Count);
        foreach (var client in clients)
        {
            resourcesTasks.Add(client.SafeListResourcesAsync(cancellationToken));
            resourceTemplatesTasks.Add(client.SafeListResourceTemplatesAsync(cancellationToken));
        }

        await Register(resources, resourcesTasks, static resource => resource.Uri);
        await Register(resourceTemplates, resourceTemplatesTasks, static resourceTemplate => resourceTemplate.UriTemplate);

        listResourcesResultTask = Task.FromResult(new ListResourcesResult
        {
            Resources = resources.Select(static entry => entry.Value.Resource).ToList()
        });

        listResourceTemplatesResultTask = Task.FromResult(new ListResourceTemplatesResult
        {
            ResourceTemplates = resourceTemplates.Select(static entry => entry.Value.ResourceTemplate).ToList()
        });

        if (runningServer is { } server)
            await server.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification, cancellationToken);
    }

    private async Task InitializeTools(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        tools.Clear();

        var toolsTasks = new List<Task<IList<McpClientTool>>>(clients.Count);
        foreach (var client in clients)
            toolsTasks.Add(client.SafeListToolsAsync(null, cancellationToken));

        await Register(tools, toolsTasks, static tool => tool.Name);

        listToolsResultTask = Task.FromResult(new ListToolsResult
        {
            Tools = tools.Select(static entry => entry.Value.Tool.ProtocolTool).ToList()
        });

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

    private async Task Register<T>(
        Dictionary<string, (IMcpClient, T)> registry,
        List<Task<IList<T>>> tasks,
        Func<T, string> keySelector)
    {
        var clientsItems = await Task.WhenAll(tasks);
        for (var index = 0; index < clientsItems.Length; index++)
        {
            var client = Clients[index];
            var clientItems = clientsItems[index];
            foreach (var clientItem in clientItems)
                registry[keySelector(clientItem)] = (client, clientItem);
        }
    }

    private static (IMcpClient Client, T) Find<T>(Dictionary<string, (IMcpClient, T)> registry, string type, string? name)
    {
        if (name is null)
            throw new McpException($"Missing {type} name");

        if (!registry.TryGetValue(name, out var item))
            throw new McpException($"Unknown {type}: '{name}'");

        return item;
    }
}