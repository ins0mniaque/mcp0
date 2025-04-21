using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models;

internal static class Utf8Json
{
    public static string GetPropertyName(this ref Utf8JsonReader reader)
    {
        return reader.GetString() ?? throw new JsonException("'null' is an invalid property name");
    }

    public static T Deserialize<T>(this ref Utf8JsonReader reader, JsonTypeInfo<T> jsonTypeInfo)
    {
        return JsonSerializer.Deserialize(ref reader, jsonTypeInfo) ??
               throw new JsonException($"'null' is an invalid {typeof(T).Name}");
    }
}