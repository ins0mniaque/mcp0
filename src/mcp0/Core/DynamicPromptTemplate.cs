using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Core;

internal sealed class DynamicPromptTemplate(Models.Prompt prompt)
{
    public Task<List<PromptMessage>> Render<T>(IMcpServer server, IReadOnlyDictionary<string, T> arguments, CancellationToken cancellationToken)
    {
        return Render(server,
                      arguments.ToDictionary(static entry => entry.Key,
                                             static entry => entry.Value?.ToString(),
                                             StringComparer.Ordinal),
                      cancellationToken);
    }

    // TODO: Support options
    private async Task<List<PromptMessage>> Render(IMcpServer server, Dictionary<string, string?> arguments, CancellationToken cancellationToken)
    {
        var messages = new List<SamplingMessage>(prompt.Messages.Length * 2);

        foreach (var message in prompt.Messages)
        {
            var content = new Content { Text = Template.Render(message.Template, arguments) };

            messages.Add(new SamplingMessage { Role = Role.User, Content = content });
            if (message.ReturnArgument is not { } returnArgument)
                continue;

            var request = new CreateMessageRequestParams { Messages = messages };
            var result = await server.RequestSamplingAsync(request, cancellationToken);

            messages.Add(new SamplingMessage { Role = Role.Assistant, Content = result.Content });

            arguments[returnArgument] = result.Content.Text;
        }

        return messages.Select(static message => new PromptMessage { Role = message.Role, Content = message.Content })
                       .ToList();
    }
}