using System.ClientModel;

using Microsoft.Extensions.AI;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Types;

using OpenAI;

namespace mcp0.Core;

internal sealed class Sampling : IDisposable
{
    private IChatClient? chatClient;

    public void ConfigureOllama(Uri? endpoint, string? model)
    {
        ChatClient = new OllamaChatClient(endpoint ?? new Uri("http://localhost:11434/"), model ?? "llama3.1");
    }

    public void ConfigureOpenAI(Uri? endpoint, string? apiKey, string? model)
    {
        var credential = new ApiKeyCredential(apiKey ?? string.Empty);
        var options = endpoint is null ? null : new OpenAIClientOptions { Endpoint = endpoint };

        ChatClient = new OpenAI.Chat.ChatClient(model ?? "gpt-4o-mini", credential, options).AsChatClient();
    }

    public IChatClient? ChatClient
    {
        get => chatClient;
        private set
        {
            chatClient?.Dispose();
            chatClient = value;
        }
    }

    public void Dispose()
    {
        chatClient?.Dispose();
        chatClient = null;
    }

    public static ValueTask<CreateMessageResult> EmulatedSamplingHandler(
        CreateMessageRequestParams? request,
        IProgress<ProgressNotificationValue> progress,
        CancellationToken cancellationToken)
    {
        var model = "default model";
        if (request?.ModelPreferences?.Hints is { Count: not 0 } hints)
            model = hints[0].Name ?? model;

        return ValueTask.FromResult(new CreateMessageResult
        {
            Model = model,
            Role = Role.Assistant,
            Content = new() { Text = $"[Emulated sampling from {model}]" }
        });
    }
}