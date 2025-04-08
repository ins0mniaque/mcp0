using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Core;

internal sealed class Server
{
    public static string Name { get; } = typeof(Server).Assembly.GetName().Name ?? "mcp0";
    public static string Version { get; } = typeof(Server).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static string NameFrom(IEnumerable<string> names) => string.Join('/', names.DefaultIfEmpty(Name));

    private readonly Dictionary<string, (IMcpClient Client, McpClientPrompt Prompt)> prompts;
    private readonly Dictionary<string, (IMcpClient Client, Resource Resource)> resources;
    private readonly Dictionary<string, (IMcpClient Client, ResourceTemplate ResourceTemplate)> resourceTemplates;
    private readonly Dictionary<string, (IMcpClient Client, McpClientTool Tool)> tools;
    private readonly ConcurrentDictionary<IMcpClient, byte> disabledCompletionClients = new();
    private readonly string name;
    private readonly string version;
    private readonly ILoggerFactory loggerFactory;

    private IMcpServer? runningServer;
    private Task<ListPromptsResult> listPromptsResultTask = Task.FromResult(new ListPromptsResult());
    private Task<ListResourcesResult> listResourcesResultTask = Task.FromResult(new ListResourcesResult());
    private Task<ListResourceTemplatesResult> listResourceTemplatesResultTask = Task.FromResult(new ListResourceTemplatesResult());
    private Task<ListToolsResult> listToolsResultTask = Task.FromResult(new ListToolsResult());

    public Server(string name, string version, ILoggerFactory loggerFactory)
    {
        Prompts = prompts = new(StringComparer.Ordinal);
        Resources = resources = new(StringComparer.Ordinal);
        ResourceTemplates = resourceTemplates = new(StringComparer.Ordinal);
        Tools = tools = new(StringComparer.Ordinal);

        this.name = name;
        this.version = version;
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

        await InitializePrompts(clients, cancellationToken);
        await InitializeResources(clients, cancellationToken);
        await InitializeTools(clients, cancellationToken);
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        if (runningServer is not null)
            throw new InvalidOperationException("Server is already running");

        var serverOptions = GetServerOptions();

        await using var transport = new StdioServerTransport(name, loggerFactory);
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

    public McpClientOptions GetClientOptions() => new()
    {
        Capabilities = new()
        {
            NotificationHandlers = new Dictionary<string, Func<JsonRpcNotification, Task>>(StringComparer.Ordinal)
            {
                { NotificationMethods.PromptListChangedNotification, _ => InitializePrompts(Clients, CancellationToken.None) },
                { NotificationMethods.ResourceListChangedNotification, _ => InitializeResources(Clients, CancellationToken.None) },
                { NotificationMethods.ToolListChangedNotification, _ => InitializeTools(Clients, CancellationToken.None) }
            },
            Sampling = new()
            {
                SamplingHandler = async (request, _, cancellationToken) =>
                {
                    ArgumentNullException.ThrowIfNull(request);

                    if (runningServer is null)
                        throw new McpException("Server is not running");

                    return await runningServer.RequestSamplingAsync(request, cancellationToken);
                }
            },
            Roots = new()
            {
                RootsHandler = async (request, cancellationToken) =>
                {
                    ArgumentNullException.ThrowIfNull(request);

                    if (runningServer is null)
                        throw new McpException("Server is not running");

                    return await runningServer.RequestRootsAsync(request, cancellationToken);
                },
                ListChanged = true
            }
        }
    };

    private McpServerOptions GetServerOptions()
    {
        var emptyCompleteResult = new CompleteResult();

        return new McpServerOptions
        {
            ServerInfo = new() { Name = name, Version = version },
            Capabilities = new()
            {
                NotificationHandlers = new Dictionary<string, Func<JsonRpcNotification, Task>>(StringComparer.Ordinal)
                {
                    {
                        NotificationMethods.RootsUpdatedNotification, async _ =>
                        {
                            var notifyTasks = new List<Task>(Clients.Count);
                            foreach (var client in Clients)
                                notifyTasks.Add(client.SendNotificationAsync(NotificationMethods.RootsUpdatedNotification));

                            await Task.WhenAll(notifyTasks);
                        }
                    }
                },
                Logging = new()
                {
                    SetLoggingLevelHandler = async (request, cancellationToken) =>
                    {
                        if (request.Params?.Level is not { } level)
                            throw new McpException("Missing logging level parameter");

                        Log.SetMinimumLevel(level switch
                        {
                            LoggingLevel.Debug => LogLevel.Debug,
                            LoggingLevel.Info => LogLevel.Information,
                            LoggingLevel.Notice => LogLevel.Information,
                            LoggingLevel.Warning => LogLevel.Warning,
                            LoggingLevel.Error => LogLevel.Error,
                            LoggingLevel.Critical => LogLevel.Critical,
                            LoggingLevel.Alert => LogLevel.Critical,
                            LoggingLevel.Emergency => LogLevel.Critical,
                            _ => LogLevel.Warning
                        });

                        var setLoggingLevelTasks = new List<Task>(Clients.Count);
                        foreach (var client in Clients)
                            setLoggingLevelTasks.Add(client.SafeSetLoggingLevel(level, cancellationToken));

                        await Task.WhenAll(setLoggingLevelTasks);

                        return new();
                    }
                },
                Prompts = new()
                {
                    ListPromptsHandler = (_, _) => listPromptsResultTask,
                    GetPromptHandler = async (request, cancellationToken) =>
                    {
                        var (_, prompt) = Find(prompts, "prompt", request.Params?.Name);
                        var arguments = Convert(request.Params?.Arguments);

                        return await prompt.GetAsync(arguments, null, cancellationToken);
                    }
                },
                Resources = new()
                {
                    ListResourcesHandler = (_, _) => listResourcesResultTask,
                    ListResourceTemplatesHandler = (_, _) => listResourceTemplatesResultTask,
                    ReadResourceHandler = async (request, cancellationToken) =>
                    {
                        var (client, resource) = Find(resources, "resource", request.Params?.Uri);

                        return await client.ReadResourceAsync(resource.Uri, cancellationToken);
                    },
                    SubscribeToResourcesHandler = async (request, cancellationToken) =>
                    {
                        var (client, resource) = Find(resources, "resource", request.Params?.Uri);

                        await client.SubscribeToResourceAsync(resource.Uri, cancellationToken);

                        return new();
                    },
                    UnsubscribeFromResourcesHandler = async (request, cancellationToken) =>
                    {
                        var (client, resource) = Find(resources, "resource", request.Params?.Uri);

                        await client.UnsubscribeFromResourceAsync(resource.Uri, cancellationToken);

                        return new();
                    }
                },
                Tools = new()
                {
                    ListToolsHandler = (_, _) => listToolsResultTask,
                    CallToolHandler = async (request, cancellationToken) =>
                    {
                        var (client, tool) = Find(tools, "tool", request.Params?.Name);
                        var arguments = Convert(request.Params?.Arguments);

                        return await client.CallToolAsync(tool.Name, arguments, null, cancellationToken);
                    }
                }
            },
            GetCompletionHandler = async (request, cancellationToken) =>
            {
                IMcpClient client;
                if (request.Params?.Ref.Uri is { } resourceUri)
                {
                    if (resources.TryGetValue(resourceUri, out var resourceEntry))
                        client = resourceEntry.Client;
                    else if (resourceTemplates.TryGetValue(resourceUri, out var resourceTemplateEntry))
                        client = resourceTemplateEntry.Client;
                    else
                        throw new McpException($"Unknown resource or resource template: '{resourceUri}'");
                }
                else if (request.Params?.Ref.Name is { } promptName)
                {
                    if (prompts.TryGetValue(promptName, out var promptEntry))
                        client = promptEntry.Client;
                    else
                        throw new McpException($"Unknown prompt: '{promptName}'");
                }
                else
                    throw new McpException($"Missing completion request parameters");

                if (disabledCompletionClients.ContainsKey(client))
                    return emptyCompleteResult;

                var completionTask = client.GetCompletionAsync(
                    request.Params.Ref,
                    request.Params.Argument.Name,
                    request.Params.Argument.Value,
                    cancellationToken);

                return await completionTask.CatchMethodNotFound(_ =>
                {
                    disabledCompletionClients.AddOrUpdate(client, 0, static (_, _) => 0);

                    return emptyCompleteResult;
                });
            }
        };
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

    private static Dictionary<string, object?>? Convert(Dictionary<string, JsonElement>? arguments)
    {
        return arguments?.ToDictionary(
            static entry => entry.Key,
            static entry => (object?)entry.Value,
            StringComparer.Ordinal);
    }

    private static Dictionary<string, object?>? Convert(Dictionary<string, object>? arguments)
    {
        return arguments?.ToDictionary(
            static entry => entry.Key,
            static entry => (object?)entry.Value,
            StringComparer.Ordinal);
    }
}