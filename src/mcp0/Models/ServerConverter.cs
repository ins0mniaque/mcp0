using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

internal sealed class ServerConverter : JsonConverter<Server>
{
    private static class Property
    {
        public const string Name = "name";
        public const string Command = "command";
        public const string Args = "args";
        public const string WorkDir = "workDir";
        public const string Env = "env";
        public const string EnvFile = "envFile";
        public const string ShutdownTimeout = "shutdownTimeout";
        public const string Url = "url";
        public const string Headers = "headers";
        public const string ConnectionTimeout = "connectionTimeout";

        public static bool IsStdioServerProperty(string propertyName)
        {
            return propertyName is Command or Args or WorkDir or Env or EnvFile or ShutdownTimeout;
        }

        public static bool IsSseServerProperty(string propertyName)
        {
            return propertyName is Url or Headers or ConnectionTimeout;
        }
    }

    public override bool CanConvert(Type typeToConvert) => typeof(Server).IsAssignableFrom(typeToConvert);

    public override Server Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
            return Server.Parse(reader.GetString() ?? throw Exceptions.InvalidServerJson());

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw Exceptions.InvalidServerJson();

        reader.Read();
        var propertyName = reader.GetString() ?? throw Exceptions.InvalidServerJson();

        var name = (string?)null;
        if (propertyName is Property.Name)
        {
            reader.Read();
            name = reader.GetString();

            reader.Read();
            propertyName = reader.GetString() ?? throw Exceptions.InvalidServerJson();
        }

        if (Property.IsStdioServerProperty(propertyName))
            return ReadStdioServer(ref reader, propertyName, name);

        if (Property.IsSseServerProperty(propertyName))
            return ReadSseServer(ref reader, propertyName, name);

        throw Exceptions.UnknownServerProperty(propertyName);
    }

    private static StdioServer ReadStdioServer(ref Utf8JsonReader reader, string propertyName, string? name)
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
                propertyName = reader.GetString() ?? throw Exceptions.InvalidServerJson();
                ReadProperty(ref reader);
                continue;
            }

            if (reader.TokenType is not JsonTokenType.EndObject)
                throw Exceptions.InvalidServerJson();

            if (command is not null && arguments is null && StdioServer.TryParse(command) is { } server)
            {
                command = server.Command;
                arguments = server.Arguments;
                environment = Collection.Merge(environment, server.Environment);
            }

            return new StdioServer
            {
                Name = name,
                Command = command ?? throw Exceptions.MissingRequiredServerProperty(Property.Command),
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                Environment = environment?.Count is 0 ? null : environment,
                EnvironmentFile = environmentFile,
                ShutdownTimeout = shutdownTimeout
            };
        }

        throw Exceptions.InvalidServerJson();

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
                if (propertyName is Property.Name)
                    name = reader.GetString();
                else if (propertyName is Property.Command)
                    command = reader.GetString() ?? throw Exceptions.MissingRequiredServerProperty(Property.Command);
                else if (propertyName is Property.WorkDir)
                    workingDirectory = reader.GetString();
                else if (propertyName is Property.EnvFile)
                    environmentFile = reader.GetString();
                else if (propertyName is Property.ShutdownTimeout)
                    shutdownTimeout = TimeSpan.FromSeconds(reader.GetInt32());
                else
                    throw Exceptions.UnknownServerProperty(propertyName);
            }
        }
    }

    private static SseServer ReadSseServer(ref Utf8JsonReader reader, string propertyName, string? name)
    {
        var url = (Uri?)null;
        var headers = (Dictionary<string, string>?)null;
        var connectionTimeout = (TimeSpan?)null;

        ReadProperty(ref reader);

        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.PropertyName)
            {
                propertyName = reader.GetString() ?? throw Exceptions.InvalidServerJson();
                ReadProperty(ref reader);
                continue;
            }

            if (reader.TokenType is not JsonTokenType.EndObject)
                throw Exceptions.InvalidServerJson();

            return new SseServer
            {
                Name = name,
                Url = url ?? throw Exceptions.MissingRequiredServerProperty(Property.Url),
                Headers = headers,
                ConnectionTimeout = connectionTimeout
            };
        }

        throw Exceptions.InvalidServerJson();

        void ReadProperty(ref Utf8JsonReader reader)
        {
            if (propertyName is Property.Headers)
                headers = JsonSerializer.Deserialize(ref reader, ModelContext.Default.DictionaryStringString);
            else
            {
                reader.Read();
                if (propertyName is Property.Name)
                    name = reader.GetString();
                else if (propertyName is Property.Url)
                {
                    var urlString = reader.GetString();
                    if (!Uri.TryCreate(urlString, UriKind.Absolute, out url))
                        throw Exceptions.InvalidValueForServerProperty(Property.Url, urlString);
                }
                else if (propertyName is Property.ConnectionTimeout)
                    connectionTimeout = TimeSpan.FromSeconds(reader.GetInt32());
                else
                    throw Exceptions.UnknownServerProperty(propertyName);
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
            throw Exceptions.UnknownServerType(server.GetType());
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
        {
            writer.WriteStartObject();

            if (server.Name is not null)
                writer.WriteString(Property.Name, server.Name);

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
        var stringFormattable = server.Name is null &&
                                server.Headers is null &&
                                server.ConnectionTimeout is null;

        if (stringFormattable)
        {
            writer.WriteStringValue(server.Url.ToString());
        }
        else
        {
            writer.WriteStartObject();

            if (server.Name is not null)
                writer.WriteString(Property.Name, server.Name);

            writer.WriteString(Property.Url, server.Url.ToString());

            if (server.Headers is not null)
            {
                writer.WritePropertyName(Property.Headers);
                JsonSerializer.Serialize(writer, server.Headers, ModelContext.Default.DictionaryStringString);
            }

            if (server.ConnectionTimeout is not null)
                writer.WriteNumber(Property.ConnectionTimeout, server.ConnectionTimeout.Value.TotalSeconds);

            writer.WriteEndObject();
        }
    }

    private static class Exceptions
    {
        public static JsonException InvalidServerJson() => throw new JsonException("Invalid JSON in server configuration");
        public static JsonException InvalidValueForServerProperty(string propertyName, string? propertyValue) => throw new JsonException($"Invalid value for server property: {propertyName} = {propertyValue}");
        public static JsonException MissingRequiredServerProperty(string propertyName) => throw new JsonException($"Missing required server property: {propertyName}");
        public static JsonException UnknownServerProperty(string propertyName) => throw new JsonException($"Unknown server property: {propertyName}");
        public static JsonException UnknownServerType(Type serverType) => throw new JsonException($"Unknown server type: {serverType.Name}");
    }
}