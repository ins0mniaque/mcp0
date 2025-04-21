using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

internal sealed class PatchConverter : JsonConverter<Patch>
{
    public override bool HandleNull => true;

    public override Patch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.False or JsonTokenType.Null)
            return Patch.Remove;

        if (reader.TokenType is JsonTokenType.String)
            return reader.GetString() is { } text ? Patch.Parse(text) : Patch.Remove;

        return reader.Deserialize(ConverterContext.Default.Patch);
    }

    public override void Write(Utf8JsonWriter writer, Patch patch, JsonSerializerOptions options)
    {
        if (Patch.Format(patch) is { } formatted)
            writer.WriteStringValue(formatted);
        else
            writer.WriteBooleanValue(false);
    }
}