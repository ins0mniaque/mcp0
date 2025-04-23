using System.Text.Json;
using System.Text.Json.Nodes;

using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal sealed class CommandLineTool
{
    public CommandLineTool(string name, Models.Tool tool)
    {
        Tool = new()
        {
            Name = name,
            Description = tool.Description,
            InputSchema = ParseInputSchema(tool.Command)
        };

        Template = new CommandLineTemplate(tool.Command);
    }

    public Tool Tool { get; }
    public CommandLineTemplate Template { get; }

    private static JsonElement ParseInputSchema(string template)
    {
        var requiredProperties = new List<JsonNode?>();
        var properties = new JsonObject();
        foreach (var property in Core.Template.Parse(template, CreateArgument))
            properties.Add(property);

        var inputSchema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new JsonArray(requiredProperties.ToArray())
        };

        return inputSchema.Deserialize(JsonSchemaContext.Default.JsonElement);

        KeyValuePair<string, JsonNode?> CreateArgument(string name, string? type, string? description, bool required)
        {
            var argument = new JsonObject { ["type"] = TypeAlias.ToJsonSchema(type) };

            if (description is not null)
                argument["description"] = description;

            if (required)
                requiredProperties.Add(JsonValue.Create(name));

            return new(name, argument);
        }
    }
}