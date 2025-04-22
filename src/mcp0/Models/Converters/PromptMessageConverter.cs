using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

internal sealed class PromptMessageConverter : JsonConverter<PromptMessage>
{
    public override PromptMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
            return PromptMessage.Parse(reader.GetString() ?? throw new JsonException("Expected a string or object for the prompt message configuration"));

        return reader.Deserialize(ConverterContext.Default.PromptMessage);
    }

    public override void Write(Utf8JsonWriter writer, PromptMessage message, JsonSerializerOptions options)
    {
        if (PromptMessage.TryFormat(message) is { } formatted)
            writer.WriteStringValue(formatted);
        else
            JsonSerializer.Serialize(writer, message, ConverterContext.Default.PromptMessage);
    }
}