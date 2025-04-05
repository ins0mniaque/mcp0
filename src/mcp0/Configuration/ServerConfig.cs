using System.Text.Json.Serialization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol.Transport;

internal sealed class ServerConfig
{
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("args")]
    public List<string>? Arguments { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string>? Environment { get; set; }

    [JsonPropertyName("envFile")]
    public Dictionary<string, string>? EnvironmentFile { get; set; }

    [JsonPropertyName("url")]
    public Uri? Url { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    public McpServerConfig ToMcp(string serverName) => Url switch
    {
        null => ToMcpStdIo(serverName),
        _ => ToMcpSse(serverName)
    };

    private McpServerConfig ToMcpStdIo(string serverName)
    {
        if (Url is not null || Headers is not null)
            throw new InvalidOperationException("Server with command does not support URL or Headers");

        var config = new McpServerConfig
        {
            Id = serverName,
            Name = serverName,
            TransportType = TransportTypes.StdIo,
            TransportOptions = new()
            {
                ["command"] = Command ?? throw new InvalidOperationException("command is empty"),
                ["arguments"] = string.Join(' ', Arguments ?? [])
            }
        };

        if (Environment is { } environment)
            foreach (var variable in environment)
                config.TransportOptions["env:" + variable.Key] = variable.Value;

        return config;
    }

    private McpServerConfig ToMcpSse(string serverName)
    {
        if (Command is not null || Arguments is not null || Environment is not null || EnvironmentFile is not null)
            throw new InvalidOperationException("Server with URL does not support Command, Arguments, Environment or EnvironmentFile");

        var config = new McpServerConfig
        {
            Id = serverName,
            Name = serverName,
            TransportType = TransportTypes.Sse,
            TransportOptions = new()
            {
                ["url"] = Url?.ToString() ?? throw new InvalidOperationException("url is empty")
            }
        };

        if (Headers is { } headers)
            foreach (var header in headers)
                config.TransportOptions["header:" + header.Key] = header.Value;

        return config;
    }
}
