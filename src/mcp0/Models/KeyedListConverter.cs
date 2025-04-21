using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models;

internal abstract class KeyedListConverter<T> : JsonConverter<List<T>>
{
    protected abstract JsonTypeInfo<T> JsonTypeInfo { get; }

    protected abstract Func<T, string?> GetKey { get; }
    protected abstract Func<T, string?, T> SetKey { get; }

    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.StartArray)
        {
            var list = new List<T>();

            while (reader.TokenType is not JsonTokenType.EndArray)
            {
                reader.Read();
                list.Add(reader.Deserialize(JsonTypeInfo));
            }

            return list;
        }

        if (reader.TokenType is not JsonTokenType.StartObject)
        {
            var list = new List<T>();

            while (reader.TokenType is not JsonTokenType.EndObject)
            {
                reader.Read();
                var propertyName = reader.GetPropertyName();

                reader.Read();
                var element = reader.Deserialize(JsonTypeInfo);

                list.Add(SetKey(element, propertyName));
            }

            reader.Read();

            return list;
        }

        throw new JsonException("Expected array or object");
    }

    public override void Write(Utf8JsonWriter writer, List<T> list, JsonSerializerOptions options)
    {
        if (list.Count is 0)
        {
            writer.WriteNullValue();
        }
        else if (list.All(element => GetKey(element) is not null))
        {
            writer.WriteStartObject();

            foreach (var element in list)
            {
                var name = GetKey(element)!;

                writer.WritePropertyName(name);

                SetKey(element, null);
                JsonSerializer.Serialize(writer, element, JsonTypeInfo);
                SetKey(element, name);
            }

            writer.WriteEndObject();
        }
        else
        {
            writer.WriteStartArray();

            foreach (var element in list)
                JsonSerializer.Serialize(writer, element, JsonTypeInfo);

            writer.WriteEndArray();
        }
    }
}