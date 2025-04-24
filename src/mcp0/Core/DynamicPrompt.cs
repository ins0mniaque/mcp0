using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal sealed class DynamicPrompt
{
    public DynamicPrompt(Models.Prompt prompt)
    {
        Prompt = new()
        {
            Name = prompt.Name,
            Description = prompt.Description,
            Arguments = ParseArguments(prompt)
        };

        Template = new(prompt);
    }

    public Prompt Prompt { get; }
    public DynamicPromptTemplate Template { get; }

    private static List<PromptArgument>? ParseArguments(Models.Prompt prompt)
    {
        var arguments = new Dictionary<string, PromptArgument>(StringComparer.Ordinal);

        foreach (var message in prompt.Messages)
            foreach (var argument in Core.Template.Parse(message.Template, CreateArgument))
                arguments[argument.Name] = argument;

        foreach (var message in prompt.Messages)
            if (message.ReturnArgument is { } returnArgument)
                arguments.Remove(returnArgument);

        return arguments.Count is 0 ? null : arguments.Select(static entry => entry.Value).ToList();

        static PromptArgument CreateArgument(string name, string? type, string? description, bool required)
        {
            return new()
            {
                Name = name,
                Description = description,
                Required = required ? true : null
            };
        }
    }
}