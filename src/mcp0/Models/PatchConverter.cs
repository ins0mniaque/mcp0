using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

internal sealed class PatchConverter : JsonConverter<Patch>
{
    private static class Property
    {
        public const string Name = "name";
        public const string Description = "description";
    }

    public override bool HandleNull => true;

    public override bool CanConvert(Type typeToConvert) => typeof(Patch).IsAssignableFrom(typeToConvert);

    public override Patch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.False or JsonTokenType.Null)
            return Patch.Remove;

        if (reader.TokenType is JsonTokenType.String)
        {
            return Patch.FromString(reader.GetString() ?? throw Exceptions.InvalidPatchJson()) ??
                   throw Exceptions.InvalidPatchStringValue();
        }

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw Exceptions.InvalidPatchJson();

        var name = (string?)null;
        var description = (string?)null;

        while (true)
        {
            reader.Read();
            if (reader.TokenType is JsonTokenType.EndObject)
                break;

            if (reader.TokenType is not JsonTokenType.PropertyName)
                throw Exceptions.InvalidPatchJson();

            var propertyName = reader.GetString() ?? throw Exceptions.InvalidPatchJson();

            reader.Read();
            if (propertyName is Property.Name)
                name = reader.GetString();
            else if (propertyName is Property.Description)
                description = reader.GetString();
            else
                throw Exceptions.UnknownPatchProperty(propertyName);
        }

        return new() { Name = name, Description = description };
    }

    public override void Write(Utf8JsonWriter writer, Patch patch, JsonSerializerOptions options)
    {
        if (patch == Patch.Remove)
            writer.WriteBooleanValue(false);
        else if (patch.Description is null || patch.Description.Length is 0)
            writer.WriteStringValue(patch.Name);
        else if (patch.Name is null || patch.Name.Length is 0)
            writer.WriteStringValue($"# {patch.Description}");
        else
            writer.WriteStringValue($"{patch.Name} # {patch.Description}");
    }

    private static class Exceptions
    {
        public static JsonException InvalidPatchJson() => throw new JsonException("Invalid JSON in patch configuration");
        public static JsonException InvalidPatchStringValue() => throw new JsonException("Invalid string value for patch configuration");
        public static JsonException UnknownPatchProperty(string propertyName) => throw new JsonException($"Unknown patch property: {propertyName}");
    }
}