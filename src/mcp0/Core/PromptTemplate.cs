using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal static class PromptTemplate
{
    public static List<PromptArgument> Parse(string template)
    {
        return Template.Parse(template, CreateArgument).ToList();

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