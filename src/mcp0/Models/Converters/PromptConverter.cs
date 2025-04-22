using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

internal sealed class PromptConverter : JsonConverter<Prompt>
{
    public override Prompt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
            return Prompt.Parse(reader.GetString() ?? throw new JsonException("Expected a string, array or object for the prompt configuration"));

        if (reader.TokenType is JsonTokenType.StartArray)
            return new Prompt { Messages = reader.Deserialize(ModelContext.Default.PromptMessageArray) };

        return reader.Deserialize(ConverterContext.Default.Prompt);
    }

    public override void Write(Utf8JsonWriter writer, Prompt prompt, JsonSerializerOptions options)
    {
        if (Prompt.TryFormat(prompt) is { } formatted)
            writer.WriteStringValue(formatted);
        else if (prompt.Description is null && prompt.Options is null)
            JsonSerializer.Serialize(writer, prompt.Messages, ConverterContext.Default.PromptMessageArray);
        else
            JsonSerializer.Serialize(writer, prompt, ConverterContext.Default.Prompt);
    }
}