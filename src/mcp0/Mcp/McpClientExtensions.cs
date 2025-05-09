using System.Text.Json;

using Microsoft.Extensions.AI;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Types;

namespace mcp0.Mcp;

internal static class McpClientExtensions
{
    public static Func<CreateMessageRequestParams?, IProgress<ProgressNotificationValue>, CancellationToken, ValueTask<CreateMessageResult>> CreateSamplingWithModelHandler(
        this IChatClient chatClient)
    {
        var defaultSamplingHandler = chatClient.CreateSamplingHandler();

        return async (request, progress, cancellationToken) =>
        {
            var samplingHandler = defaultSamplingHandler;
            if (request?.ModelPreferences?.Hints is { Count: not 0 } hints && hints[0].Name is { } model)
                samplingHandler = new ChatClientWithModel(chatClient, model).CreateSamplingHandler();

            return await samplingHandler(request, progress, cancellationToken);
        };
    }

    private sealed class ChatClientWithModel(IChatClient chatClient, string modelId) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
        {
            return chatClient.GetResponseAsync(messages, AddModel(options), cancellationToken);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
        {
            return chatClient.GetStreamingResponseAsync(messages, AddModel(options), cancellationToken);
        }

        private ChatOptions AddModel(ChatOptions? options)
        {
            options ??= new();
            options.ModelId ??= modelId;

            return options;
        }

        public object? GetService(Type serviceType, object? serviceKey) => chatClient.GetService(serviceType, serviceKey);
        public void Dispose() => chatClient.Dispose();
    }

    public static Task<IList<McpClientPrompt>> SafeListPromptsAsync(
        this IMcpClient client, CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities.Prompts is null)
            return Task.FromResult<IList<McpClientPrompt>>([]);

        return client.ListPromptsAsync(cancellationToken)
            .CatchMethodNotFound(static _ => []);
    }

    public static Task<IList<Resource>> SafeListResourcesAsync(
        this IMcpClient client, CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities.Resources is null)
            return Task.FromResult<IList<Resource>>([]);

        return client.ListResourcesAsync(cancellationToken)
            .CatchMethodNotFound(static _ => []);
    }

    public static Task<IList<ResourceTemplate>> SafeListResourceTemplatesAsync(
        this IMcpClient client, CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities.Resources is null)
            return Task.FromResult<IList<ResourceTemplate>>([]);

        return client.ListResourceTemplatesAsync(cancellationToken)
            .CatchMethodNotFound(static _ => []);
    }

    public static Task SafeSubscribeToResourceAsync(
        this IMcpClient client, string uri, CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities.Resources?.Subscribe is not true)
            return Task.CompletedTask;

        return client.SubscribeToResourceAsync(uri, cancellationToken)
            .CatchMethodNotFound(static _ => { });
    }

    public static Task SafeUnsubscribeFromResourceAsync(
        this IMcpClient client, string uri, CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities.Resources?.Subscribe is not true)
            return Task.CompletedTask;

        return client.UnsubscribeFromResourceAsync(uri, cancellationToken)
            .CatchMethodNotFound(static _ => { });
    }

    public static Task<IList<McpClientTool>> SafeListToolsAsync(
        this IMcpClient client,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities.Tools is null)
            return Task.FromResult<IList<McpClientTool>>([]);

        return client.ListToolsAsync(serializerOptions, cancellationToken)
            .CatchMethodNotFound(static _ => []);
    }

    private static readonly CompleteResult emptyCompleteResult = new();

    public static Task<CompleteResult> SafeCompleteAsync(
        this IMcpClient client,
        Reference reference, string argumentName, string argumentValue,
        CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities.Completions is null)
            return Task.FromResult(emptyCompleteResult);

        return client.CompleteAsync(reference, argumentName, argumentValue, cancellationToken)
            .CatchMethodNotFound(static _ => emptyCompleteResult);
    }

    public static Task SafeSetLoggingLevel(
        this IMcpClient client,
        LoggingLevel level,
        CancellationToken cancellationToken = default)
    {
        if (client.ServerCapabilities.Logging is null)
            return Task.CompletedTask;

        return client.SetLoggingLevel(level, cancellationToken)
            .CatchMethodNotFound(static _ => { });
    }

    private static async Task CatchMethodNotFound(this Task task, Action<McpException> fallback)
    {
        try
        {
            await task;
        }
        catch (McpException exception) when (exception.ErrorCode is McpErrorCode.MethodNotFound)
        {
            fallback(exception);
        }
    }

    private static async Task<T> CatchMethodNotFound<T>(this Task<T> task, Func<McpException, T> fallback)
    {
        try
        {
            return await task;
        }
        catch (McpException exception) when (exception.ErrorCode is McpErrorCode.MethodNotFound)
        {
            return fallback(exception);
        }
    }
}