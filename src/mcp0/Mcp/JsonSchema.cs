using System.Text.Json;

internal static class JsonSchema
{
    public static JsonSchemaNode Unknown { get; } = new JsonSchemaSymbol("unknown");

    public static JsonSchemaNode Parse(JsonElement element) => Parse(element, true);

    private static JsonSchemaNode Parse(JsonElement element, bool isRequired)
    {
        if (element.ValueKind is JsonValueKind.Null)
            return new JsonSchemaSymbol("null");

        if (element.ValueKind is JsonValueKind.String && element.GetString() is { } type)
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

        if (element.TryGetProperty("type", out JsonElement typeElement))
        {
            if (typeElement.ValueKind is JsonValueKind.Array)
                return new JsonSchemaUnionType(typeElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

            if (typeElement.ValueKind is JsonValueKind.String && typeElement.GetString() is { } complexType)
                return complexType switch
                {
                    "array" => ParseArray(element, isRequired),
                    "object" => ParseObject(element, isRequired),
                    _ => Parse(typeElement, isRequired)
                };
        }

        if (element.TryGetProperty("enum", out JsonElement enumElement) && enumElement.ValueKind is JsonValueKind.Array)
            return new JsonSchemaUnionType(enumElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

        if (element.TryGetProperty("const", out JsonElement constElement))
            return new JsonSchemaUnionType([Parse(constElement)], isRequired);

        if (element.TryGetProperty("anyOf", out JsonElement anyOfElement) && anyOfElement.ValueKind is JsonValueKind.Array)
            return new JsonSchemaUnionType(anyOfElement.EnumerateArray().Select(Parse).ToArray(), isRequired);

        return Unknown;
    }

    private static JsonSchemaArrayType ParseArray(JsonElement element, bool isRequired)
    {
        if (!element.TryGetProperty("items", out JsonElement itemsElement))
            return new JsonSchemaArrayType(Unknown, isRequired);

        return new(Parse(itemsElement), isRequired);
    }

    private static JsonSchemaObjectType ParseObject(JsonElement element, bool isRequired)
    {
        if (!element.TryGetProperty("properties", out JsonElement propertiesElement) || propertiesElement.ValueKind is not JsonValueKind.Object)
            return new(Array.Empty<JsonSchemaProperty>(), isRequired);

        var required = new HashSet<string>();
        if (element.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
            foreach (var propertyElement in requiredElement.EnumerateArray())
                if (propertyElement.ValueKind is JsonValueKind.String && propertyElement.GetString() is { } property)
                    required.Add(property);

        var properties = propertiesElement.EnumerateObject().Select(ParseProperty).ToArray();

        return new(properties, isRequired);

        JsonSchemaProperty ParseProperty(JsonProperty property)
        {
            return new(property.Name, Parse(property.Value, required.Contains(property.Name)));
        }
    }
}

internal abstract record JsonSchemaNode;
internal abstract record JsonSchemaType(bool IsRequired) : JsonSchemaNode;
internal sealed record JsonSchemaSymbol(string Name) : JsonSchemaNode;
internal sealed record JsonSchemaPrimitiveType(string Name, bool IsRequired) : JsonSchemaType(IsRequired);
internal sealed record JsonSchemaArrayType(JsonSchemaNode ElementType, bool IsRequired) : JsonSchemaType(IsRequired);

internal sealed record JsonSchemaProperty(string Name, JsonSchemaNode Type);
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

internal sealed record JsonSchemaUnionType(JsonSchemaNode[] UnionTypes, bool IsRequired) : JsonSchemaType(IsRequired)
{
    public bool Equals(JsonSchemaUnionType? other)
    {
        return other is not null
            && EqualityContract == other.EqualityContract
            && EqualityComparer<bool>.Default.Equals(IsRequired, other.IsRequired)
            && UnionTypes.SequenceEqual(other.UnionTypes, EqualityComparer<JsonSchemaNode>.Default);
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
