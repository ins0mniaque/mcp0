using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal sealed class DynamicPrompt
{
    public DynamicPrompt(string name, string template, string? description)
    {
        Prompt = new()
        {
            Name = name,
            Description = description,
            Arguments = ParseArguments(template)
        };

        Template = new(template);
    }

    public Prompt Prompt { get; }
    public DynamicPromptTemplate Template { get; }

    private static List<PromptArgument>? ParseArguments(string template)
    {
        var arguments = Core.Template.Parse(template, CreateArgument).ToList();

        return arguments.Count is 0 ? null : arguments;

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