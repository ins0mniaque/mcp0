using System.Text.Json;

internal abstract record JsonSchemaNode;
internal abstract record JsonSchemaType(bool IsRequired) : JsonSchemaNode;
internal sealed record JsonSchemaProperty(string Name, JsonSchemaNode Type);
internal sealed record JsonSchemaObjectType(JsonSchemaProperty[] Properties, bool IsRequired) : JsonSchemaType(IsRequired);
internal sealed record JsonSchemaArrayType(JsonSchemaNode ElementType, bool IsRequired) : JsonSchemaType(IsRequired);
internal sealed record JsonSchemaUnionType(JsonSchemaNode[] UnionTypes, bool IsRequired) : JsonSchemaType(IsRequired);
internal sealed record JsonSchemaPrimitiveType(string Name, bool IsRequired) : JsonSchemaType(IsRequired);
internal sealed record JsonSchemaSymbol(string Name) : JsonSchemaNode;

internal static class JsonSchema
{
    public static JsonSchemaNode Unknown { get; } = new JsonSchemaSymbol("unknown");

    public static JsonSchemaNode Parse(JsonElement element) => Parse(element, true);

    private static JsonSchemaNode Parse(JsonElement element, bool isRequired)
    {
        if (element.TryGetProperty("type", out JsonElement typeElement))
        {
            if (typeElement.ValueKind is JsonValueKind.Array)
                return new JsonSchemaUnionType(typeElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

            if (typeElement.ValueKind is JsonValueKind.String && typeElement.GetString() is { } type)
                return type switch
                {
                    "boolean" or
                    "number" or
                    "string" or
                    "integer" => new JsonSchemaPrimitiveType(type, isRequired),
                    "array" => ParseArray(element, isRequired),
                    "object" => ParseObject(element, isRequired),
                    _ => new JsonSchemaSymbol(type)
                };
        }

        if (element.TryGetProperty("enum", out JsonElement enumElement) && enumElement.ValueKind is JsonValueKind.Array)
            return new JsonSchemaUnionType(enumElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

        if (element.TryGetProperty("anyOf", out JsonElement anyOfElement))
        {
            if (anyOfElement.ValueKind is JsonValueKind.Array)
                return new JsonSchemaUnionType(anyOfElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

            if (anyOfElement.ValueKind is JsonValueKind.Object)
                return new JsonSchemaUnionType(anyOfElement.EnumerateObject().Select(static o => Parse(o.Value)).ToArray(), isRequired);
        }

        return Unknown;
    }

    private static JsonSchemaArrayType ParseArray(JsonElement element, bool isRequired)
    {
        if (!element.TryGetProperty("items", out JsonElement itemsElement))
            return new JsonSchemaArrayType(Unknown, isRequired);

        return new JsonSchemaArrayType(Parse(itemsElement), isRequired);
    }

    private static JsonSchemaObjectType ParseObject(JsonElement element, bool isRequired)
    {
        if (!element.TryGetProperty("properties", out JsonElement propertiesElement) || propertiesElement.ValueKind is not JsonValueKind.Object)
            return new JsonSchemaObjectType(Array.Empty<JsonSchemaProperty>(), isRequired);

        var required = new HashSet<string>();
        if (element.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
            foreach (var propertyElement in requiredElement.EnumerateArray())
                if (propertyElement.ValueKind is JsonValueKind.String && propertyElement.GetString() is { } property)
                    required.Add(property);

        var properties = propertiesElement.EnumerateObject().Select(ParseProperty).ToArray();

        return new JsonSchemaObjectType(properties, isRequired);

        JsonSchemaProperty ParseProperty(JsonProperty property)
        {
            return new JsonSchemaProperty(property.Name, Parse(property.Value, required.Contains(property.Name)));
        }
    }
}
