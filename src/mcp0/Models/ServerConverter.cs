using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

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
            Write(writer, stdioServer);
        else if (server is SseServer sseServer)
            Write(writer, sseServer);
        else
            throw new JsonException($"Unknown server type: {server.GetType().Name}");
    }

    private static readonly SearchValues<char> commandDelimiters = SearchValues.Create(' ', '\"', '\'');

    private static void Write(Utf8JsonWriter writer, StdioServer server)
    {
        var stringFormattable = server.Name is null &&
                                server.WorkingDirectory is null &&
                                server.Environment is null &&
                                server.EnvironmentFile is null &&
                                server.ShutdownTimeout is null &&
                                server.Command.AsSpan().ContainsAny(commandDelimiters) is false &&
                                server.Arguments?.Any(a => a.AsSpan().ContainsAny(commandDelimiters)) is false or null;

        if (stringFormattable)
        {
            if (server.Arguments is null || server.Arguments.Length is 0)
                writer.WriteStringValue(server.Command);
            else
                writer.WriteStringValue(server.Command + ' ' + string.Join(' ', server.Arguments));
        }
        else
            JsonSerializer.Serialize(writer, server, ConverterContext.Default.StdioServer);
    }

    private static void Write(Utf8JsonWriter writer, SseServer server)
    {
        var stringFormattable = server.Name is null &&
                                server.Headers is null &&
                                server.ConnectionTimeout is null;

        if (stringFormattable)
            writer.WriteStringValue(server.Url.ToString());
        else
            JsonSerializer.Serialize(writer, server, ConverterContext.Default.SseServer);
    }
}