using System.Text.Json;

internal static class McpToolInputSchema
{
    public static string GetSignature(JsonElement element)
    {
        var required = new HashSet<string>();
        if (element.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
            foreach (var propertyElement in requiredElement.EnumerateArray())
                if (propertyElement.GetString() is { } property)
                    required.Add(property);

        var arguments = element.GetProperty("properties")
            .EnumerateObject()
            .Select(s => s.Name + ":" + ParseType(s.Value) + (required.Contains(s.Name) ? string.Empty : "?"));

        return string.Join(", ", arguments);
    }

    public static string ParseType(JsonElement element)
    {
        if (element.TryGetProperty("type", out JsonElement typeElement) && typeElement.GetString() is { } type)
        {
            return type switch
            {
                "array" => $"{ParseType(element.GetProperty("items"))}[]",
                "string" => "str",
                "integer" => "int",
                "number" => "num",
                "boolean" => "bool",
                _ => type
            };
        }

        if (element.TryGetProperty("enum", out JsonElement enumElement) && enumElement.ValueKind == JsonValueKind.Array)
        {
            return $"[{string.Join('|', enumElement.EnumerateArray())}";
        }

        return "unknown";
    }
}
