using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Core;

internal sealed class DynamicPromptTemplate(Models.Prompt prompt)
{
    public Task<List<PromptMessage>> Render<T>(IMcpServer server, IReadOnlyDictionary<string, T> arguments, ProgressToken? progressToken, CancellationToken cancellationToken)
    {
        return Render(server,
                      arguments.ToDictionary(static entry => entry.Key,
                                             static entry => entry.Value?.ToString(),
                                             StringComparer.Ordinal),
                      progressToken,
                      cancellationToken);
    }

    private async Task<List<PromptMessage>> Render(IMcpServer server, Dictionary<string, string?> arguments, ProgressToken? progressToken, CancellationToken cancellationToken)
    {
        FormatArguments(arguments);

        var messages = new List<SamplingMessage>(prompt.Messages.Length * 2);

        foreach (var message in prompt.Messages)
        {
            var content = new Content { Text = Template.Render(message.Template, arguments) };

            messages.Add(new SamplingMessage { Role = Role.User, Content = content });
            if (message.ReturnArgument is not { } returnArgument)
                continue;

            var model = message.Options?.Model ?? prompt.Options?.Model;
            var modelPreferences = model is null ? null : new ModelPreferences
            {
                Hints = model.Select(static model => new ModelHint { Name = model }).ToList()
            };

            var request = new CreateMessageRequestParams
            {
                Messages = messages,
                ModelPreferences = modelPreferences,
                IncludeContext = (message.Options?.Context ?? prompt.Options?.Context) switch
                {
                    Models.PromptContext.None => ContextInclusion.None,
                    Models.PromptContext.Server => ContextInclusion.ThisServer,
                    Models.PromptContext.AllServers => ContextInclusion.AllServers,
                    _ => null
                },
                SystemPrompt = message.Options?.SystemPrompt ?? prompt.Options?.SystemPrompt,
                MaxTokens = message.Options?.MaxTokens ?? prompt.Options?.MaxTokens ?? -1,
                StopSequences = message.Options?.StopSequences ?? prompt.Options?.StopSequences,
                Temperature = message.Options?.Temperature ?? prompt.Options?.Temperature,
                Meta = progressToken is null ? null : new() { ProgressToken = progressToken }
            };

            var result = await server.RequestSamplingAsync(request, cancellationToken);

            messages.Add(new SamplingMessage { Role = Role.Assistant, Content = result.Content });

            arguments[returnArgument] = FormatArgument(returnArgument, result.Content.Text);
        }

        return messages.Select(static message => new PromptMessage { Role = message.Role, Content = message.Content })
                       .ToList();
    }

    private static void FormatArguments(Dictionary<string, string?> arguments)
    {
        foreach (var entry in arguments.ToList())
            arguments[entry.Key] = FormatArgument(entry.Key, entry.Value);
    }

    private static string? FormatArgument(string key, string? argument)
    {
        if (argument is null)
            return null;

        if (argument.Contains('\n'))
            return $"\n<{key}>\n{argument}\n</{key}>\n";

        return $"<{key}>{argument}</{key}>";
    }
}