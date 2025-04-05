using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

internal sealed class Server
{
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

    public IReadOnlyDictionary<string, (IMcpClient Client, McpClientPrompt Prompt)> Prompts { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, Resource Resource)> Resources { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, ResourceTemplate ResourceTemplate)> ResourceTemplates { get; }
    public IReadOnlyDictionary<string, (IMcpClient Client, McpClientTool Tool)> Tools { get; }

    // TODO: Handle tool changed event (see IMcpClient.AddNotificationHandler/XXXCapability.ListChanged)
    public async Task Initialize(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
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
            if (client.ServerCapabilities?.Prompts is not null)
                listPromptsTasks.Add(client.ListPromptsAsync(cancellationToken));
            else
                listPromptsTasks.Add(Task.FromResult<IList<McpClientPrompt>>(new List<McpClientPrompt>()));

            if (client.ServerCapabilities?.Resources is not null)
                listResourcesTasks.Add(client.ListResourcesAsync(cancellationToken));
            else
                listResourcesTasks.Add(Task.FromResult<IList<Resource>>(new List<Resource>()));

            if (client.ServerCapabilities?.Resources is not null)
                listResourceTemplatesTasks.Add(client.ListResourceTemplatesAsync(cancellationToken));
            else
                listResourceTemplatesTasks.Add(Task.FromResult<IList<ResourceTemplate>>(new List<ResourceTemplate>()));

            if (client.ServerCapabilities?.Tools is not null)
                listToolsTasks.Add(client.ListToolsAsync(null, cancellationToken));
            else
                listToolsTasks.Add(Task.FromResult<IList<McpClientTool>>(new List<McpClientTool>()));
        }

        var clientsPrompts = await Task.WhenAll(listPromptsTasks);
        for (var index = 0; index < clientsPrompts.Length; index++)
        {
            var client = clients[index];
            var clientPrompts = clientsPrompts[index];
            foreach (var clientPrompt in clientPrompts)
                prompts[clientPrompt.Name] = (client, clientPrompt);
        }

        var clientsResources = await Task.WhenAll(listResourcesTasks);
        for (var index = 0; index < clientsResources.Length; index++)
        {
            var client = clients[index];
            var clientResources = clientsResources[index];
            foreach (var clientResource in clientResources)
                resources[clientResource.Uri] = (client, clientResource);
        }

        var clientsResourceTemplates = await Task.WhenAll(listResourceTemplatesTasks);
        for (var index = 0; index < clientsResourceTemplates.Length; index++)
        {
            var client = clients[index];
            var clientResourceTemplates = clientsResourceTemplates[index];
            foreach (var clientResourceTemplate in clientResourceTemplates)
                resourceTemplates[clientResourceTemplate.UriTemplate] = (client, clientResourceTemplate);
        }

        var clientsTools = await Task.WhenAll(listToolsTasks);
        for (var index = 0; index < clientsTools.Length; index++)
        {
            var client = clients[index];
            var clientTools = clientsTools[index];
            foreach (var clientTool in clientTools)
                tools[clientTool.Name] = (client, clientTool);
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

        var options = new McpServerOptions
        {
            ServerInfo = new() { Name = name, Version = version },
            Capabilities = new()
            {
                Logging = new()
                {
                    SetLoggingLevelHandler = (request, cancellationToken) =>
                    {
                        // TODO: Set minimum log level based on request.Params?.Level
                        return Task.FromResult(new EmptyResult());
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

                return await client.GetCompletionAsync(request.Params.Ref, request.Params.Argument.Name, request.Params.Argument.Value, cancellationToken);
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
