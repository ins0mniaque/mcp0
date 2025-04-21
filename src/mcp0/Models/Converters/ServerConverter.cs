using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

internal sealed class ServerConverter : JsonConverter<Server>
{
    private static bool IsServerProperty(string propertyName) => propertyName is "name";
    private static bool IsSseServerProperty(string propertyName) => propertyName is "url" or "headers" or "connectionTimeout";

    public override bool CanConvert(Type typeToConvert) => typeof(Server).IsAssignableFrom(typeToConvert);

    public override Server Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String && reader.GetString() is { } text)
            return Server.Parse(text);

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new JsonException("Expected a string or an object for the server configuration");

        var snapshot = reader;

        string propertyName;
        while (true)
        {
            reader.Read();
            propertyName = reader.GetPropertyName();
            if (!IsServerProperty(propertyName))
                break;

            reader.Read();
        }

        reader = snapshot;

        if (IsSseServerProperty(propertyName))
            return reader.Deserialize(ConverterContext.Default.SseServer);

        return reader.Deserialize(ConverterContext.Default.StdioServer);
    }

    public override void Write(Utf8JsonWriter writer, Server server, JsonSerializerOptions options)
    {
        if (server is StdioServer stdioServer)
        {
            if (StdioServer.TryFormat(stdioServer) is { } formatted)
                writer.WriteStringValue(formatted);
            else
                JsonSerializer.Serialize(writer, stdioServer, ConverterContext.Default.StdioServer);
        }
        else if (server is SseServer sseServer)
        {
            if (SseServer.TryFormat(sseServer) is { } formatted)
                writer.WriteStringValue(formatted);
            else
                JsonSerializer.Serialize(writer, sseServer, ConverterContext.Default.SseServer);
        }
        else
            throw new JsonException($"Unknown server type: {server.GetType().Name}");
    }
}