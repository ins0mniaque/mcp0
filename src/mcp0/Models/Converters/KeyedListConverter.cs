using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models.Converters;

internal abstract class KeyedListConverter<T> : JsonConverter<List<T>>
{
    protected abstract JsonTypeInfo<T> JsonTypeInfo { get; }

    protected abstract Func<T, string?> GetKey { get; }
    protected abstract Action<T, string?> SetKey { get; }

    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.StartArray)
        {
            var list = new List<T>();

            reader.Read();
            while (reader.TokenType is not JsonTokenType.EndArray)
            {
                list.Add(reader.Deserialize(JsonTypeInfo));

                reader.Read();
            }

            return list;
        }

        if (reader.TokenType is JsonTokenType.StartObject)
        {
            var list = new List<T>();

            reader.Read();
            while (reader.TokenType is not JsonTokenType.EndObject)
            {
                var propertyName = reader.GetPropertyName();

                reader.Read();

                var element = reader.Deserialize(JsonTypeInfo);
                SetKey(element, propertyName);
                list.Add(element);

                reader.Read();
            }

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