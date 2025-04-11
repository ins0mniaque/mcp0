using System.Text.Json;
using System.Text.Json.Nodes;

namespace mcp0.Core;

internal static class ToolTemplate
{
    public static JsonElement Parse(string template)
    {
        var requiredProperties = new List<JsonNode?>();
        var properties = new JsonObject();
        foreach (var property in Template.Parse(template, CreateArgument))
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
            var argument = new JsonObject { ["type"] = type ?? "string" };

            if (description is not null)
                argument["description"] = description;

            if (required)
                requiredProperties.Add(JsonValue.Create(name));

            return new(name, argument);
        }
    }
}