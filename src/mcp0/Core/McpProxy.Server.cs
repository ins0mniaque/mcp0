using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Core;

internal sealed partial class McpProxy
{
    private McpServerOptions GetServerOptions()
    {
        var emptyCompleteResult = new CompleteResult();

        return new McpServerOptions
        {
            ServerInfo = proxyOptions.ServerInfo ?? new() { Name = Name, Version = Version },
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

                        proxyOptions.SetLoggingLevelCallback?.Invoke(level);

                        await SetLoggingLevel(level, cancellationToken);

                        return new();
                    }
                },
                Prompts = new()
                {
                    ListPromptsHandler = (_, _) => listPromptsResultTask,
                    GetPromptHandler = async (request, cancellationToken) =>
                    {
                        var (_, prompt) = Find(prompts, "prompt", request.Params?.Name);
                        var arguments = request.Params?.Arguments?.ToDictionary(
                            static entry => entry.Key,
                            static entry => (object?)entry.Value,
                            StringComparer.Ordinal);

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

                        await client.SafeSubscribeToResourceAsync(resource.Uri, cancellationToken);

                        return new();
                    },
                    UnsubscribeFromResourcesHandler = async (request, cancellationToken) =>
                    {
                        var (client, resource) = Find(resources, "resource", request.Params?.Uri);

                        await client.SafeUnsubscribeFromResourceAsync(resource.Uri, cancellationToken);

                        return new();
                    }
                },
                Tools = new()
                {
                    ListToolsHandler = (_, _) => listToolsResultTask,
                    CallToolHandler = async (request, cancellationToken) =>
                    {
                        var (client, tool) = Find(tools, "tool", request.Params?.Name);
                        var arguments = request.Params?.Arguments?.ToDictionary(
                            static entry => entry.Key,
                            static entry => (object?)entry.Value,
                            StringComparer.Ordinal);

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
                    client = Find(prompts, "prompt", promptName).Client;
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
}