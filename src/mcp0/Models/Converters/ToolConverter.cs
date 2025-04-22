using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

internal sealed class ToolConverter : JsonConverter<Tool>
{
    public override Tool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
            return Tool.Parse(reader.GetString() ?? throw new JsonException("Expected a string or object for the tool configuration"));

        return reader.Deserialize(ConverterContext.Default.Tool);
    }

    public override void Write(Utf8JsonWriter writer, Tool tool, JsonSerializerOptions options)
    {
        if (Tool.TryFormat(tool) is { } formatted)
            writer.WriteStringValue(formatted);
        else
            JsonSerializer.Serialize(writer, tool, ConverterContext.Default.Tool);
    }
}