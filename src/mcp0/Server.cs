using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

internal sealed class Server
{
    public static string Name { get; } = typeof(Server).Assembly.GetName()?.Name ?? "mcp0";
    public static string Version { get; } = typeof(Server).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public static string NameFrom(IEnumerable<string> names) => string.Join('/', names.DefaultIfEmpty(Name));

    private readonly Dictionary<string, (IMcpClient Client, McpClientPrompt Prompt)> prompts;
    private readonly Dictionary<string, (IMcpClient Client, Resource Resource)> resources;
    private readonly Dictionary<string, (IMcpClient Client, ResourceTemplate ResourceTemplate)> resourceTemplates;
    private readonly Dictionary<string, (IMcpClient Client, McpClientTool Tool)> tools;
    private readonly string name;
    private readonly string version;
    private readonly ILoggerFactory loggerFactory;

    public Server(string name, string version, ILoggerFactory loggerFactory)
    {
        Prompts = prompts = new();
        Resources = resources = new();
        ResourceTemplates = resourceTemplates = new();
        Tools = tools = new();

        this.name = name;
        this.version = version;
        this.loggerFactory = loggerFactory;
    }

    public IReadOnlyList<IMcpClient> Clients { get; private set; } = [];
    public IReadOnlyDictionary<string, (IMcpClient Client, McpClientPrompt Prompt)> Prompts { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, Resource Resource)> Resources { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, ResourceTemplate ResourceTemplate)> ResourceTemplates { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, McpClientTool Tool)> Tools { get; }

    // TODO: Handle changed events (see IMcpClient.AddNotificationHandler/XXXCapability.ListChanged)
    public async Task Initialize(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        Clients = clients;

        prompts.Clear();
        resources.Clear();
        resourceTemplates.Clear();
        tools.Clear();

        var listPromptsTasks = new List<Task<IList<McpClientPrompt>>>(clients.Count);
        var listResourcesTasks = new List<Task<IList<Resource>>>(clients.Count);
        var listResourceTemplatesTasks = new List<Task<IList<ResourceTemplate>>>(clients.Count);
        var listToolsTasks = new List<Task<IList<McpClientTool>>>(clients.Count);

        foreach (var client in clients)
        {
            listPromptsTasks.Add(client.SafeListPromptsAsync(cancellationToken));
            listResourcesTasks.Add(client.SafeListResourcesAsync(cancellationToken));
            listResourceTemplatesTasks.Add(client.SafeListResourceTemplatesAsync(cancellationToken));
            listToolsTasks.Add(client.SafeListToolsAsync(null, cancellationToken));
        }

        await Register(prompts, listPromptsTasks, prompt => prompt.Name);
        await Register(resources, listResourcesTasks, resource => resource.Uri);
        await Register(resourceTemplates, listResourceTemplatesTasks, resourceTemplate => resourceTemplate.UriTemplate);
        await Register(tools, listToolsTasks, tool => tool.Name);

        async Task Register<T>(Dictionary<string, (IMcpClient, T)> dictionary, List<Task<IList<T>>> tasks, Func<T, string> keySelector)
        {
            var clientsItems = await Task.WhenAll(tasks);
            for (var index = 0; index < clientsItems.Length; index++)
            {
                var client = clients[index];
                var clientItems = clientsItems[index];
                foreach (var clientItem in clientItems)
                    dictionary[keySelector(clientItem)] = (client, clientItem);
            }
        }
    }

    public async Task Serve(CancellationToken cancellationToken)
    {
        var listPromptsResultTask = Task.FromResult(new ListPromptsResult
        {
            Prompts = prompts.Select(entry => entry.Value.Prompt.ProtocolPrompt).ToList()
        });

        var listResourcesResultTask = Task.FromResult(new ListResourcesResult
        {
            Resources = resources.Select(entry => entry.Value.Resource).ToList()
        });

        var listResourceTemplatesResultTask = Task.FromResult(new ListResourceTemplatesResult
        {
            ResourceTemplates = resourceTemplates.Select(entry => entry.Value.ResourceTemplate).ToList()
        });

        var listToolsResultTask = Task.FromResult(new ListToolsResult
        {
            Tools = tools.Select(entry => entry.Value.Tool.ProtocolTool).ToList()
        });

        var disabledCompletionClients = new ConcurrentDictionary<IMcpClient, byte>();
        var emptyCompleteResult = new CompleteResult();

        var options = new McpServerOptions
        {
            ServerInfo = new() { Name = name, Version = version },
            Capabilities = new()
            {
                Logging = new()
                {
                    SetLoggingLevelHandler = async (request, cancellationToken) =>
                    {
                        if (request.Params?.Level is not { } level)
                            throw new McpServerException("Missing logging level parameter");

                        // TODO: Set minimum log level based on request.Params?.Level

                        var setLoggingLevelTasks = new List<Task>(Clients.Count);
                        foreach (var client in Clients)
                            setLoggingLevelTasks.Add(client.SafeSetLoggingLevel(level, cancellationToken));

                        await Task.WhenAll(setLoggingLevelTasks);

                        return new();
                    }
                },
                Prompts = new()
                {
                    ListPromptsHandler = (request, cancellationToken) => listPromptsResultTask,
                    GetPromptHandler = async (request, cancellationToken) =>
                    {
                        var (client, prompt) = Find(prompts, "prompt", request.Params?.Name);
                        var arguments = Convert(request.Params?.Arguments);

                        return await prompt.GetAsync(arguments, null, cancellationToken);
                    }
                },
                Resources = new()
                {
                    ListResourcesHandler = (request, cancellationToken) => listResourcesResultTask,
                    ListResourceTemplatesHandler = (request, cancellationToken) => listResourceTemplatesResultTask,
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
                    ListToolsHandler = (request, cancellationToken) => listToolsResultTask,
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
                if (request.Params?.Ref.Uri is { } uri)
                {
                    if (resources.TryGetValue(uri, out var resourceEntry))
                        client = resourceEntry.Client;
                    else if (resourceTemplates.TryGetValue(uri, out var resourceTemplateEntry))
                        client = resourceTemplateEntry.Client;
                    else
                        throw new McpServerException($"Unknown resource or resource template: '{uri}'");
                }
                else if (request.Params?.Ref.Name is { } name)
                {
                    if (prompts.TryGetValue(name, out var promptEntry))
                        client = promptEntry.Client;
                    else
                        throw new McpServerException($"Unknown prompt: '{name}'");
                }
                else
                    throw new McpServerException($"Missing completion request parameters");

                if (disabledCompletionClients.ContainsKey(client))
                    return emptyCompleteResult;

                return await client.GetCompletionAsync(request.Params.Ref, request.Params.Argument.Name, request.Params.Argument.Value, cancellationToken).CatchMethodNotFound(exception =>
                {
                    disabledCompletionClients.AddOrUpdate(client, default(byte), (_, _) => default);

                    return emptyCompleteResult;
                });
            }
        };

        await using var transport = new StdioServerTransport(name, loggerFactory);
        await using IMcpServer server = McpServerFactory.Create(transport, options, loggerFactory);

        await server.RunAsync(cancellationToken);
    }

    private static (IMcpClient Client, T) Find<T>(Dictionary<string, (IMcpClient, T)> registry, string type, string? name)
    {
        if (name is null)
            throw new McpServerException($"Missing {type} name");

        if (!registry.TryGetValue(name, out var item))
            throw new McpServerException($"Unknown {type}: '{name}'");

        return item;
    }

    private static Dictionary<string, object?>? Convert(Dictionary<string, JsonElement>? arguments)
    {
        return arguments?.ToDictionary(entry => entry.Key, entry => (object?)entry.Value);
    }

    private static Dictionary<string, object?>? Convert(Dictionary<string, object>? arguments)
    {
        return arguments?.ToDictionary(entry => entry.Key, entry => (object?)entry.Value);
    }
}
