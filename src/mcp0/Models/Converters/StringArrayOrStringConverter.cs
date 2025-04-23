using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

internal sealed class StringArrayOrStringConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
            return [reader.GetString() ?? throw new JsonException("Expected a string or array of strings")];

        return reader.Deserialize(ConverterContext.Default.StringArray);
    }

    public override void Write(Utf8JsonWriter writer, string[] array, JsonSerializerOptions options)
    {
        if (array.Length is 0)
            writer.WriteNullValue();
        else if (array.Length is 1)
            writer.WriteStringValue(array[0]);
        else
            JsonSerializer.Serialize(writer, array, ConverterContext.Default.StringArray);
    }
}