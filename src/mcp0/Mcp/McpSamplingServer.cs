using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Mcp;

[SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty", Justification = "Unused IMcpServer property")]
internal sealed class McpSamplingServer(Func<CreateMessageRequestParams?, string> sample) : IMcpServer
{
    public ClientCapabilities? ClientCapabilities { get; } = new() { Sampling = new() };
    public Implementation? ClientInfo { get; }
    public McpServerOptions ServerOptions { get; } = new();
    public IServiceProvider? Services { get; }
    public LoggingLevel? LoggingLevel { get; }

    public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Method is not RequestMethods.SamplingCreateMessage)
            throw new NotImplementedException();

        var requestParams = request.Params?.Deserialize(McpJsonSerializerContext.Default.CreateMessageRequestParams);

        return await Task.FromResult(new JsonRpcResponse
        {
            Id = request.Id,
            Result = JsonSerializer.SerializeToNode(new CreateMessageResult
            {
                Model = "model",
                Role = Role.Assistant,
                Content = new() { Text = sample(requestParams) }
            }, McpJsonSerializerContext.Default.CreateMessageResult),
        });
    }

    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public IAsyncDisposable RegisterNotificationHandler(string method, Func<JsonRpcNotification, CancellationToken, ValueTask> handler) => throw new NotImplementedException();
    public Task RunAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}