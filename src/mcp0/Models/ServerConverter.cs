using System.Buffers;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

internal sealed class ServerConverter : JsonConverter<Server>
{
    private static class Property
    {
        public const string Command = "command";
        public const string Args = "args";
        public const string WorkDir = "workDir";
        public const string Env = "env";
        public const string EnvFile = "envFile";
        public const string ShutdownTimeout = "shutdownTimeout";
        public const string Url = "url";
        public const string Headers = "headers";
        public const string ConnectionTimeout = "connectionTimeout";
        public const string MaxReconnectAttempts = "maxReconnectAttempts";
        public const string ReconnectDelay = "reconnectDelay";

        public static bool IsStdioServerProperty(string propertyName)
        {
            return propertyName is Command or Args or WorkDir or Env or EnvFile or ShutdownTimeout;
        }

        public static bool IsSseServerProperty(string propertyName)
        {
            return propertyName is Url or Headers or ConnectionTimeout or MaxReconnectAttempts or ReconnectDelay;
        }
    }

    public override bool CanConvert(Type typeToConvert) => typeof(Server).IsAssignableFrom(typeToConvert);

    public override Server Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
        {
            var commandOrUrl = reader.GetString();
            if (Uri.IsWellFormedUriString(commandOrUrl, UriKind.Absolute))
                return new SseServer { Url = new Uri(commandOrUrl) };

            if (!string.IsNullOrWhiteSpace(commandOrUrl))
            {
                var commandLine = CommandLineStringSplitter.Instance.Split(commandOrUrl).ToArray();
                if (commandLine.Length is not 0)
                {
                    return new StdioServer
                    {
                        Command = commandLine[0],
                        Arguments = commandLine.Length is 0 ? null : commandLine[1..]
                    };
                }
            }

            throw new JsonException("Invalid string value for server configuration");
        }

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new JsonException("Invalid JSON in server configuration");

        reader.Read();
        if (reader.TokenType is not JsonTokenType.PropertyName)
            throw new JsonException("Invalid JSON in server configuration");

        var propertyName = reader.GetString() ?? throw new JsonException("Invalid JSON in server configuration");

        if (Property.IsStdioServerProperty(propertyName))
            return ReadStdioServer(ref reader, propertyName);

        if (Property.IsSseServerProperty(propertyName))
            return ReadSseServer(ref reader, propertyName);

        throw new JsonException($"Unknown server property: {propertyName}");
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
            if (reader.TokenType is JsonTokenType.PropertyName)
            {
                propertyName = reader.GetString() ?? throw new JsonException("Invalid JSON in server configuration");
                ReadProperty(ref reader);
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject)
            {
                if (command is not null && arguments is null)
                {
                    arguments = CommandLineStringSplitter.Instance.Split(command).ToArray();
                    command = arguments.Length is 0 ? null : arguments[0];
                    arguments = arguments.Length is 0 ? null : arguments[1..];
                }

                return new StdioServer
                {
                    Command = command ?? throw new JsonException("Missing required server property: command"),
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    Environment = environment,
                    EnvironmentFile = environmentFile,
                    ShutdownTimeout = shutdownTimeout
                };
            }

            throw new JsonException("Invalid JSON in server configuration");
        }

        throw new JsonException("Invalid JSON in server configuration");

        void ReadProperty(ref Utf8JsonReader reader)
        {
            if (propertyName is Property.Args)
            {
                arguments = JsonSerializer.Deserialize(ref reader, ModelContext.Default.StringArray);
            }
            else if (propertyName is Property.Env)
            {
                environment = JsonSerializer.Deserialize(ref reader, ModelContext.Default.DictionaryStringString);
            }
            else
            {
                reader.Read();
                if (propertyName is Property.Command)
                    command = reader.GetString() ?? throw new JsonException("Missing required server property: command");
                else if (propertyName is Property.WorkDir)
                    workingDirectory = reader.GetString();
                else if (propertyName is Property.EnvFile)
                    environmentFile = reader.GetString();
                else if (propertyName is Property.ShutdownTimeout)
                    shutdownTimeout = TimeSpan.FromSeconds(reader.GetInt32());
                else
                    throw new JsonException($"Unknown server property: {propertyName}");
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
            if (reader.TokenType is JsonTokenType.PropertyName)
            {
                propertyName = reader.GetString() ?? throw new JsonException("Invalid JSON in server configuration");
                ReadProperty(ref reader);
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject)
            {
                return new SseServer
                {
                    Url = url ?? throw new JsonException("Missing required server property: url"),
                    Headers = headers,
                    ConnectionTimeout = connectionTimeout,
                    MaxReconnectAttempts = maxReconnectAttempts,
                    ReconnectDelay = reconnectDelay
                };
            }

            throw new JsonException("Invalid JSON in server configuration");
        }

        throw new JsonException("Invalid JSON in server configuration");

        void ReadProperty(ref Utf8JsonReader reader)
        {
            if (propertyName is Property.Headers)
                headers = JsonSerializer.Deserialize(ref reader, ModelContext.Default.DictionaryStringString);
            else
            {
                reader.Read();
                if (propertyName is Property.Url)
                {
                    if (!Uri.TryCreate(reader.GetString(), UriKind.Absolute, out url))
                        throw new JsonException("Invalid URL for required server property: url");
                }
                else if (propertyName is Property.ConnectionTimeout)
                    connectionTimeout = TimeSpan.FromSeconds(reader.GetInt32());
                else if (propertyName is Property.MaxReconnectAttempts)
                    maxReconnectAttempts = reader.GetInt32();
                else if (propertyName is Property.ReconnectDelay)
                    reconnectDelay = TimeSpan.FromSeconds(reader.GetInt32());
                else
                    throw new JsonException($"Unknown server property: {propertyName}");
            }
        }
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
        var stringFormattable = server.WorkingDirectory is null &&
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
        {
            writer.WriteStartObject();

            writer.WriteString(Property.Command, server.Command);

            if (server.Arguments is not null && server.Arguments.Length > 0)
            {
                writer.WritePropertyName(Property.Args);
                JsonSerializer.Serialize(writer, server.Arguments, ModelContext.Default.StringArray);
            }

            if (server.WorkingDirectory is not null)
                writer.WriteString(Property.WorkDir, server.WorkingDirectory);

            if (server.Environment is not null)
            {
                writer.WritePropertyName(Property.Env);
                JsonSerializer.Serialize(writer, server.Environment, ModelContext.Default.DictionaryStringString);
            }

            if (server.EnvironmentFile is not null)
                writer.WriteString(Property.EnvFile, server.EnvironmentFile);

            if (server.ShutdownTimeout.HasValue)
                writer.WriteNumber(Property.ShutdownTimeout, server.ShutdownTimeout.Value.TotalSeconds);

            writer.WriteEndObject();
        }
    }

    private static void Write(Utf8JsonWriter writer, SseServer server)
    {
        var stringFormattable = server.Headers is null &&
                                server.ConnectionTimeout is null &&
                                server.MaxReconnectAttempts is null &&
                                server.ReconnectDelay is null;

        if (stringFormattable)
        {
            writer.WriteStringValue(server.Url.ToString());
        }
        else
        {
            writer.WriteStartObject();

            writer.WriteString(Property.Url, server.Url.ToString());

            if (server.Headers is not null)
            {
                writer.WritePropertyName(Property.Headers);
                JsonSerializer.Serialize(writer, server.Headers, ModelContext.Default.DictionaryStringString);
            }

            if (server.ConnectionTimeout is not null)
                writer.WriteNumber(Property.ConnectionTimeout, server.ConnectionTimeout.Value.TotalSeconds);

            if (server.MaxReconnectAttempts is not null)
                writer.WriteNumber(Property.MaxReconnectAttempts, server.MaxReconnectAttempts.Value);

            if (server.ReconnectDelay is not null)
                writer.WriteNumber(Property.ReconnectDelay, server.ReconnectDelay.Value.TotalSeconds);

            writer.WriteEndObject();
        }
    }
}