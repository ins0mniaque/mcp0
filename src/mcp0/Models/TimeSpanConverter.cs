using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

internal sealed class TimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return TimeSpan.FromSeconds(reader.GetInt32());
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan timeSpan, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((int)timeSpan.TotalSeconds);
    }
}