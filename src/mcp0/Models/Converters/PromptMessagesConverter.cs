using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

internal sealed class PromptMessagesConverter : JsonConverter<PromptMessage[]>
{
    public override PromptMessage[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.Deserialize(ModelContext.Default.PromptMessageArray);
    }

    public override void Write(Utf8JsonWriter writer, PromptMessage[] messages, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, messages, ModelContext.Default.PromptMessageArray);
    }
}