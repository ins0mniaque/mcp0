using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

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

    public override void Write(Utf8JsonWriter writer, Patch? patch, JsonSerializerOptions options)
    {
        if (patch is null || patch == Patch.Remove)
            writer.WriteBooleanValue(false);
        else if (patch.Description is null || patch.Description.Length is 0)
            writer.WriteStringValue(patch.Name);
        else if (patch.Name is null || patch.Name.Length is 0)
            writer.WriteStringValue($"# {patch.Description}");
        else
            writer.WriteStringValue($"{patch.Name} # {patch.Description}");
    }
}