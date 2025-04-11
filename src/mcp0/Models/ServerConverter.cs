using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

internal sealed class ServerConverter : JsonConverter<Server>
{
    public override bool CanConvert(Type typeToConvert) => typeof(Server).IsAssignableFrom(typeToConvert);

    public override Server Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var commandOrUrl = reader.GetString();
            if (Uri.IsWellFormedUriString(commandOrUrl, UriKind.Absolute))
                return new SseServer { Url = new Uri(commandOrUrl) };

            if (!string.IsNullOrWhiteSpace(commandOrUrl))
            {
                var arguments = CommandLineStringSplitter.Instance.Split(commandOrUrl).ToArray();

                return new StdioServer
                {
                    Command = arguments[0],
                    Arguments = arguments.Length is 0 ? null : arguments[1..]
                };
            }

            throw new JsonException();
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        reader.Read();
        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException();

        var propertyName = reader.GetString();
        return propertyName switch
        {
            "command" or
            "args" or
            "workDir" or
            "env" or
            "envFile" or
            "shutdownTimeout" => (Server?)ReadStdioServer(ref reader, propertyName),
            "url" or
            "headers" or
            "connectionTimeout" or
            "maxReconnectAttempts" or
            "reconnectDelay" => ReadSseServer(ref reader, propertyName),
            _ => throw new JsonException()
        } ?? throw new JsonException();
    }

    private static StdioServer ReadStdioServer(ref Utf8JsonReader reader, string propertyName)
    {
        var command = (string?)null;
        var arguments = (string[]?)null;
        var workingDirectory = (string?)null;
        var environment = (Dictionary<string, string>?)null;
        var environmentFile = (string?)null;
        var shutdownTimeout = (TimeSpan?)null;

        ReadProperty(ref reader);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                propertyName = reader.GetString() ?? throw new JsonException();
                ReadProperty(ref reader);
                continue;
            }

            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (command is not null && arguments is null)
                {
                    arguments = CommandLineStringSplitter.Instance.Split(command).ToArray();
                    command = arguments[0];
                    arguments = arguments.Length is 0 ? null : arguments[1..];
                }

                return new StdioServer
                {
                    Command = command ?? throw new JsonException(),
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    Environment = environment,
                    EnvironmentFile = environmentFile,
                    ShutdownTimeout = shutdownTimeout
                };
            }

            throw new JsonException();
        }

        throw new JsonException();

        void ReadProperty(ref Utf8JsonReader reader)
        {
            if (propertyName == "args")
            {
                arguments = JsonSerializer.Deserialize(ref reader, ModelContext.Default.StringArray);
            }
            else if (propertyName == "env")
            {
                environment = JsonSerializer.Deserialize(ref reader, ModelContext.Default.DictionaryStringString);
            }
            else
            {
                reader.Read();
                if (propertyName == "command")
                    command = reader.GetString();
                else if (propertyName == "workDir")
                    workingDirectory = reader.GetString();
                else if (propertyName == "envFile")
                    environmentFile = reader.GetString();
                else if (propertyName == "shutdownTimeout")
                    shutdownTimeout = TimeSpan.FromSeconds(reader.GetInt32());
                else
                    throw new JsonException();
            }
        }
    }

    private static SseServer ReadSseServer(ref Utf8JsonReader reader, string propertyName)
    {
        var url = (Uri?)null;
        var headers = (Dictionary<string, string>?)null;
        var connectionTimeout = (TimeSpan?)null;
        var maxReconnectAttempts = (int?)null;
        var reconnectDelay = (TimeSpan?)null;

        ReadProperty(ref reader);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                propertyName = reader.GetString() ?? throw new JsonException();
                ReadProperty(ref reader);
                continue;
            }

            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new SseServer
                {
                    Url = url ?? throw new JsonException(),
                    Headers = headers,
                    ConnectionTimeout = connectionTimeout,
                    MaxReconnectAttempts = maxReconnectAttempts,
                    ReconnectDelay = reconnectDelay
                };
            }

            throw new JsonException();
        }

        throw new JsonException();

        void ReadProperty(ref Utf8JsonReader reader)
        {
            if (propertyName == "headers")
            {
                headers = JsonSerializer.Deserialize(ref reader, ModelContext.Default.DictionaryStringString);
            }
            else
            {
                reader.Read();
                if (propertyName == "url")
                    url = Uri.TryCreate(reader.GetString(), UriKind.Absolute, out var uri) ? uri : throw new JsonException();
                else if (propertyName == "connectionTimeout")
                    connectionTimeout = TimeSpan.FromSeconds(reader.GetInt32());
                else if (propertyName == "maxReconnectAttempts")
                    maxReconnectAttempts = reader.GetInt32();
                else if (propertyName == "reconnectDelay")
                    reconnectDelay = TimeSpan.FromSeconds(reader.GetInt32());
                else
                    throw new JsonException();
            }
        }
    }

    public override void Write(Utf8JsonWriter writer, Server person, JsonSerializerOptions options)
    {
        // TODO: Implement serialization
        throw new NotImplementedException();
    }
}