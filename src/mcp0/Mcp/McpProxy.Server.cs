using System.Reflection;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Mcp;

internal sealed partial class McpProxy
{
    private static AssemblyName DefaultAssemblyName { get; } = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName();

    private static Implementation DefaultImplementation { get; } = new()
    {
        Name = DefaultAssemblyName.Name ?? nameof(McpProxy),
        Version = DefaultAssemblyName.Version?.ToString() ?? "1.0.0",
    };

    public void ConfigureServerOptions(McpServerOptions options)
    {
        options.ServerInfo = proxyOptions?.ServerInfo ?? DefaultImplementation;
        options.Capabilities = GetServerCapabilities();
    }

    public McpServerOptions GetServerOptions()
    {
        var options = new McpServerOptions();
        ConfigureServerOptions(options);
        return options;
    }

    private ServerCapabilities GetServerCapabilities() => new()
    {
        NotificationHandlers = new Dictionary<string, Func<JsonRpcNotification, CancellationToken, ValueTask>>(StringComparer.Ordinal)
        {
            [NotificationMethods.RootsUpdatedNotification] = async (_, cancellationToken) =>
            {
                var notifyTasks = new List<Task>(Clients.Count);
                foreach (var client in Clients)
                    notifyTasks.Add(client.SendNotificationAsync(NotificationMethods.RootsUpdatedNotification, cancellationToken));

                await Task.WhenAll(notifyTasks);
            }
        },
        Logging = new()
        {
            SetLoggingLevelHandler = async (request, cancellationToken) =>
            {
                Server = request.Server;

                if (request.Params?.Level is not { } level)
                    throw new McpException("Missing logging level parameter");

                proxyOptions?.SetLoggingLevelCallback?.Invoke(level);

                await SetLoggingLevel(level, cancellationToken);

                return new();
            }
        },
        Prompts = new()
        {
            ListPromptsHandler = (request, _) =>
            {
                Server = request.Server;

                return ValueTask.FromResult(listPromptsResult);
            },
            GetPromptHandler = async (request, cancellationToken) =>
            {
                Server = request.Server;

                var prompt = Prompts.Find(request.Params?.Name, out var client);
                var arguments = request.Params?.Arguments?.ToDictionary(
                    static entry => entry.Key,
                    static entry => (object?)entry.Value,
                    StringComparer.Ordinal);

                return await client.GetPromptAsync(Prompts.Unmap(prompt), arguments, cancellationToken: cancellationToken);
            }
        },
        Resources = new()
        {
            ListResourcesHandler = (request, _) =>
            {
                Server = request.Server;

                return ValueTask.FromResult(listResourcesResult);
            },
            ListResourceTemplatesHandler = (request, _) =>
            {
                Server = request.Server;

                return ValueTask.FromResult(listResourceTemplatesResult);
            },
            ReadResourceHandler = async (request, cancellationToken) =>
            {
                Server = request.Server;

                if (request.Params?.Uri is not { } uri)
                    throw new McpException("Missing resource or resource template uri");

                if (Resources.TryFind(uri, out var client, out var resource))
                    return await client.ReadResourceAsync(Resources.Unmap(resource), cancellationToken);
                if (ResourceTemplates.TryFind(uri, out client, out var resourceTemplate))
                    return await client.ReadResourceAsync(ResourceTemplates.Unmap(resourceTemplate, uri), cancellationToken);

                throw new McpException($"Unknown resource or resource template: '{uri}'");
            },
            SubscribeToResourcesHandler = async (request, cancellationToken) =>
            {
                Server = request.Server;

                if (request.Params?.Uri is not { } uri)
                    throw new McpException("Missing resource or resource template uri");

                if (Resources.TryFind(uri, out var client, out var resource))
                    await client.SafeSubscribeToResourceAsync(Resources.Unmap(resource), cancellationToken);
                else if (ResourceTemplates.TryFind(uri, out client, out var resourceTemplate))
                    await client.SafeSubscribeToResourceAsync(ResourceTemplates.Unmap(resourceTemplate, uri), cancellationToken);
                else
                    throw new McpException($"Unknown resource or resource template: '{uri}'");

                return new();
            },
            UnsubscribeFromResourcesHandler = async (request, cancellationToken) =>
            {
                Server = request.Server;

                if (request.Params?.Uri is not { } uri)
                    throw new McpException("Missing resource or resource template uri");

                if (Resources.TryFind(uri, out var client, out var resource))
                    await client.SafeUnsubscribeFromResourceAsync(Resources.Unmap(resource), cancellationToken);
                else if (ResourceTemplates.TryFind(uri, out client, out var resourceTemplate))
                    await client.SafeUnsubscribeFromResourceAsync(ResourceTemplates.Unmap(resourceTemplate, uri), cancellationToken);
                else
                    throw new McpException($"Unknown resource or resource template: '{uri}'");

                return new();
            }
        },
        Tools = new()
        {
            ListToolsHandler = (request, _) =>
            {
                Server = request.Server;

                return ValueTask.FromResult(listToolsResult);
            },
            CallToolHandler = async (request, cancellationToken) =>
            {
                Server = request.Server;

                var tool = Tools.Find(request.Params?.Name, out var client);
                var arguments = request.Params?.Arguments?.ToDictionary(
                    static entry => entry.Key,
                    static entry => (object?)entry.Value,
                    StringComparer.Ordinal);

                return await client.CallToolAsync(Tools.Unmap(tool), arguments, cancellationToken: cancellationToken);
            }
        },
        Completions = new()
        {
            CompleteHandler = async (request, cancellationToken) =>
            {
                Server = request.Server;

                if (request.Params is null)
                    throw new McpException("Missing completion request parameters");

                IMcpClient client;
                if (request.Params.Ref.Uri is { } uri)
                {
                    if (Resources.TryFind(uri, out client, out var resource))
                        request.Params.Ref.Uri = Resources.Unmap(resource);
                    else if (ResourceTemplates.TryFind(uri, out client, out var resourceTemplate))
                        request.Params.Ref.Uri = ResourceTemplates.Unmap(resourceTemplate, uri);
                    else
                        throw new McpException($"Unknown resource or resource template: '{uri}'");
                }
                else if (request.Params.Ref.Name is { } name)
                    request.Params.Ref.Name = Prompts.Unmap(Prompts.Find(name, out client));
                else
                    throw new McpException("Invalid reference type");

                return await client.SafeCompleteAsync(
                    request.Params.Ref,
                    request.Params.Argument.Name,
                    request.Params.Argument.Value,
                    cancellationToken);
            }
        }
    };
}