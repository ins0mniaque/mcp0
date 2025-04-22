using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

internal sealed class ResourceConverter : JsonConverter<Resource>
{
    public override Resource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
            return Resource.Parse(reader.GetString() ?? throw new JsonException("Expected a string or object for the resource configuration"));

        return reader.Deserialize(ConverterContext.Default.Resource);
    }

    public override void Write(Utf8JsonWriter writer, Resource resource, JsonSerializerOptions options)
    {
        if (Resource.TryFormat(resource) is { } formatted)
            writer.WriteStringValue(formatted);
        else
            JsonSerializer.Serialize(writer, resource, ConverterContext.Default.Resource);
    }
}