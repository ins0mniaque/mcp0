using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

internal sealed class ServersConverter : JsonConverter<List<Server>>
{
    public override List<Server> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.StartArray)
        {
            var servers = new List<Server>();

            while (reader.TokenType is not JsonTokenType.EndArray)
            {
                reader.Read();
                var server = JsonSerializer.Deserialize(ref reader, ModelContext.Default.Server) ??
                             throw Exceptions.InvalidServer();

                servers.Add(server);
            }

            return servers;
        }

        if (reader.TokenType is JsonTokenType.StartObject)
        {
            var servers = new List<Server>();

            while (reader.TokenType is not JsonTokenType.EndObject)
            {
                reader.Read();
                var propertyName = reader.GetString() ?? throw Exceptions.InvalidServersJson();

                reader.Read();
                var server = JsonSerializer.Deserialize(ref reader, ModelContext.Default.Server) ??
                             throw Exceptions.InvalidServer();

                server.Name ??= propertyName;
                servers.Add(server);
            }

            reader.Read();

            return servers;
        }

        throw Exceptions.InvalidServersJson();
    }

    public override void Write(Utf8JsonWriter writer, List<Server> servers, JsonSerializerOptions options)
    {
        if (servers.Count is 0)
        {
            writer.WriteNullValue();
        }
        else if (servers.All(static server => server.Name is not null))
        {
            writer.WriteStartObject();

            foreach (var server in servers)
            {
                var name = server.Name!;

                writer.WritePropertyName(name);

                server.Name = null;
                JsonSerializer.Serialize(writer, server, ModelContext.Default.Server);
                server.Name = name;
            }

            writer.WriteEndObject();
        }
        else
        {
            writer.WriteStartArray();

            foreach (var server in servers)
                JsonSerializer.Serialize(writer, server, ModelContext.Default.Server);

            writer.WriteEndArray();
        }
    }

    private static class Exceptions
    {
        public static JsonException InvalidServersJson() => throw new JsonException("Invalid JSON in servers configuration");
        public static JsonException InvalidServer() => throw new JsonException("Invalid server in servers configuration");
    }
}