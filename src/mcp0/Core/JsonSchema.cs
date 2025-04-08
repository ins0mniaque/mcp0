using System.Text.Json;

namespace mcp0.Core;

internal static class JsonSchema
{
    public static JsonSchemaSymbol Null { get; } = new JsonSchemaSymbol("null");
    public static JsonSchemaSymbol Unknown { get; } = new JsonSchemaSymbol("unknown");

    public static IJsonSchemaNode Parse(JsonElement element) => Parse(element, true);

    private static IJsonSchemaNode Parse(JsonElement element, bool isRequired)
    {
        if (element.ValueKind is JsonValueKind.Null)
            return Null;

        if (element.TryGetString(out var type))
            return type switch
            {
                "boolean" or
                "number" or
                "string" or
                "integer" => new JsonSchemaPrimitiveType(type, isRequired),
                _ => new JsonSchemaSymbol(type)
            };

        if (element.ValueKind is not JsonValueKind.Object)
            return Unknown;

        if (element.TryGetProperty("type", out var typeElement))
        {
            if (typeElement.ValueKind is JsonValueKind.Array)
                return new JsonSchemaUnionType(typeElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

            if (typeElement.TryGetString(out var complexType))
                return complexType switch
                {
                    "array" => ParseArray(element, isRequired),
                    "object" => ParseObject(element, isRequired),
                    _ => Parse(typeElement, isRequired)
                };
        }

        if (element.TryGetProperty("enum", JsonValueKind.Array, out var enumElement))
            return new JsonSchemaUnionType(enumElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

        if (element.TryGetProperty("const", out var constElement))
            return new JsonSchemaUnionType([Parse(constElement)], isRequired);

        if (element.TryGetProperty("anyOf", JsonValueKind.Array, out var anyOfElement))
            return new JsonSchemaUnionType(anyOfElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

        return Unknown;
    }

    private static JsonSchemaArrayType ParseArray(JsonElement element, bool isRequired)
    {
        if (!element.TryGetProperty("items", out var itemsElement))
            return new JsonSchemaArrayType(Unknown, isRequired);

        return new(Parse(itemsElement), isRequired);
    }

    private static JsonSchemaObjectType ParseObject(JsonElement element, bool isRequired)
    {
        if (!element.TryGetProperty("properties", JsonValueKind.Object, out var propertiesElement))
            return new([], isRequired);

        var required = new HashSet<string>(StringComparer.Ordinal);
        if (element.TryGetProperty("required", JsonValueKind.Array, out var requiredElement))
            foreach (var propertyElement in requiredElement.EnumerateArray())
                if (propertyElement.TryGetString(out var property))
                    required.Add(property);

        var properties = propertiesElement.EnumerateObject().Select(ParseProperty).ToArray();

        return new(properties, isRequired);

        JsonSchemaProperty ParseProperty(JsonProperty property)
        {
            return new(property.Name, Parse(property.Value, required.Contains(property.Name)));
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
internal abstract record JsonSchemaType(bool IsRequired) : IJsonSchemaNode;
internal sealed record JsonSchemaSymbol(string Name) : IJsonSchemaNode;
internal sealed record JsonSchemaPrimitiveType(string Name, bool IsRequired) : JsonSchemaType(IsRequired);
internal sealed record JsonSchemaArrayType(IJsonSchemaNode ElementType, bool IsRequired) : JsonSchemaType(IsRequired);

internal sealed record JsonSchemaProperty(string Name, IJsonSchemaNode Type);
internal sealed record JsonSchemaObjectType(JsonSchemaProperty[] Properties, bool IsRequired) : JsonSchemaType(IsRequired)
{
    public bool Equals(JsonSchemaObjectType? other)
    {
        return other is not null
            && EqualityContract == other.EqualityContract
            && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired)
            && Properties.SequenceEqual(other.Properties, EqualityComparer<JsonSchemaProperty>.Default);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        hashCode.Add(IsRequired);
        foreach (var property in Properties)
            hashCode.Add(property);

        return hashCode.ToHashCode();
    }
}

internal sealed record JsonSchemaUnionType(IJsonSchemaNode[] UnionTypes, bool IsRequired) : JsonSchemaType(IsRequired)
{
    public bool Equals(JsonSchemaUnionType? other)
    {
        return other is not null
            && EqualityContract == other.EqualityContract
            && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired)
            && UnionTypes.SequenceEqual(other.UnionTypes, EqualityComparer<IJsonSchemaNode>.Default);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        hashCode.Add(IsRequired);
        foreach (var unionType in UnionTypes)
            hashCode.Add(unionType);

        return hashCode.ToHashCode();
    }
}