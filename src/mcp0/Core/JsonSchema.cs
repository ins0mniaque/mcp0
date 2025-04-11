using System.Text.Json;

using Generator.Equals;

namespace mcp0.Core;

internal static class JsonSchema
{
    public static JsonSchemaSymbol Null { get; } = new("null");
    public static JsonSchemaSymbol Unknown { get; } = new("unknown");

    public static IJsonSchemaNode Parse(JsonElement element) => Parse(element, true);

    private static IJsonSchemaNode Parse(JsonElement element, bool required)
    {
        if (element.ValueKind is JsonValueKind.Null)
            return Null;

        if (element.TryGetString(out var type))
            return type switch
            {
                "boolean" or
                "number" or
                "string" or
                "integer" => new JsonSchemaPrimitiveType(type, required),
                _ => new JsonSchemaSymbol(type)
            };

        if (element.ValueKind is not JsonValueKind.Object)
            return Unknown;

        if (element.TryGetProperty("type", out var typeElement))
        {
            if (typeElement.ValueKind is JsonValueKind.Array)
                return new JsonSchemaUnionType(typeElement.EnumerateArray().Select(Parse).ToArray(), required);

            if (typeElement.TryGetString(out var complexType))
                return complexType switch
                {
                    "array" => ParseArray(element, required),
                    "object" => ParseObject(element, required),
                    _ => Parse(typeElement, required)
                };
        }

        if (element.TryGetProperty("enum", JsonValueKind.Array, out var enumElement))
            return new JsonSchemaUnionType(enumElement.EnumerateArray().Select(Parse).ToArray(), required);

        if (element.TryGetProperty("const", out var constElement))
            return new JsonSchemaUnionType([Parse(constElement)], required);

        if (element.TryGetProperty("anyOf", JsonValueKind.Array, out var anyOfElement))
            return new JsonSchemaUnionType(anyOfElement.EnumerateArray().Select(Parse).ToArray(), required);

        return Unknown;
    }

    private static JsonSchemaArrayType ParseArray(JsonElement element, bool required)
    {
        if (!element.TryGetProperty("items", out var itemsElement))
            return new JsonSchemaArrayType(Unknown, required);

        return new(Parse(itemsElement), required);
    }

    private static JsonSchemaObjectType ParseObject(JsonElement element, bool required)
    {
        if (!element.TryGetProperty("properties", JsonValueKind.Object, out var propertiesElement))
            return new([], required);

        var requiredProperties = new HashSet<string>(StringComparer.Ordinal);
        if (element.TryGetProperty("required", JsonValueKind.Array, out var requiredElement))
            foreach (var propertyElement in requiredElement.EnumerateArray())
                if (propertyElement.TryGetString(out var property))
                    requiredProperties.Add(property);

        var properties = propertiesElement.EnumerateObject().Select(ParseProperty).ToArray();

        return new(properties, required);

        JsonSchemaProperty ParseProperty(JsonProperty property)
        {
            return new(property.Name, Parse(property.Value, requiredProperties.Contains(property.Name)));
        }
    }

    private static bool TryGetProperty(this JsonElement element, string propertyName, JsonValueKind kind, out JsonElement value)
    {
        return element.TryGetProperty(propertyName, out value) && value.ValueKind == kind;
    }

    private static bool TryGetString(this JsonElement element, out string value)
    {
        if (element.ValueKind is JsonValueKind.String && element.GetString() is { } text)
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }
}

internal interface IJsonSchemaNode { }
internal abstract record JsonSchemaType(bool Required) : IJsonSchemaNode;
internal sealed record JsonSchemaSymbol(string Name) : IJsonSchemaNode;
internal sealed record JsonSchemaPrimitiveType(string Name, bool Required) : JsonSchemaType(Required);
internal sealed record JsonSchemaArrayType(IJsonSchemaNode ElementType, bool Required) : JsonSchemaType(Required);
internal sealed record JsonSchemaProperty(string Name, IJsonSchemaNode Type);

[Equatable]
internal sealed partial record JsonSchemaObjectType([property: OrderedEquality] JsonSchemaProperty[] Properties, bool Required) : JsonSchemaType(Required);

[Equatable]
internal sealed partial record JsonSchemaUnionType([property: OrderedEquality] IJsonSchemaNode[] UnionTypes, bool Required) : JsonSchemaType(Required);