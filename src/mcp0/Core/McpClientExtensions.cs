using System.Text.Json;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal static class McpClientExtensions
{
    public static async Task<IMcpClient[]> CreateMcpClientsAsync(
        this IEnumerable<McpServerConfig> servers,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var clientTasks = new List<Task<IMcpClient>>();
        foreach (var server in servers)
        {
            clientTasks.Add(McpClientFactory.CreateAsync(
                server,
                loggerFactory: loggerFactory,
                cancellationToken: cancellationToken));
        }

        return await Task.WhenAll(clientTasks);
    }

    public static Task<IList<McpClientPrompt>> SafeListPromptsAsync(
        this IMcpClient client, CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities?.Prompts is null)
            return Task.FromResult<IList<McpClientPrompt>>(new List<McpClientPrompt>());

        return client.ListPromptsAsync(cancellationToken)
            .CatchMethodNotFound(static _ => new List<McpClientPrompt>());
    }

    public static Task<IList<Resource>> SafeListResourcesAsync(
        this IMcpClient client, CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities?.Resources is null)
            return Task.FromResult<IList<Resource>>(new List<Resource>());

        return client.ListResourcesAsync(cancellationToken)
            .CatchMethodNotFound(static _ => new List<Resource>());
    }

    public static Task<IList<ResourceTemplate>> SafeListResourceTemplatesAsync(
        this IMcpClient client, CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities?.Resources is null)
            return Task.FromResult<IList<ResourceTemplate>>(new List<ResourceTemplate>());

        return client.ListResourceTemplatesAsync(cancellationToken)
            .CatchMethodNotFound(static _ => new List<ResourceTemplate>());
    }

    public static Task<IList<McpClientTool>> SafeListToolsAsync(
        this IMcpClient client,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities?.Tools is null)
            return Task.FromResult<IList<McpClientTool>>(new List<McpClientTool>());

        return client.ListToolsAsync(serializerOptions, cancellationToken)
            .CatchMethodNotFound(static _ => new List<McpClientTool>());
    }

    public static Task SafeSetLoggingLevel(
        this IMcpClient client,
        LoggingLevel level,
        CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities?.Logging is null)
            return Task.CompletedTask;

        return client.SetLoggingLevel(level, cancellationToken)
            .CatchMethodNotFound(static _ => { });
    }

    public static async Task CatchMethodNotFound(this Task task, Action<McpClientException> fallback)
    {
        const int MethodNotFoundErrorCode = -32601;

        try
        {
            await task;
        }
        catch (McpClientException exception) when (exception.ErrorCode is MethodNotFoundErrorCode)
        {
            fallback(exception);
        }
    }

    public static async Task<T> CatchMethodNotFound<T>(this Task<T> task, Func<McpClientException, T> fallback)
    {
        const int MethodNotFoundErrorCode = -32601;

        try
        {
            return await task;
        }
        catch (McpClientException exception) when (exception.ErrorCode is MethodNotFoundErrorCode)
        {
            return fallback(exception);
        }
    }
}