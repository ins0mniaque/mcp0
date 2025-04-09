using System.Globalization;
using System.Text.Json.Serialization;

using mcp0.Core;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Transport;

namespace mcp0.Model;

internal sealed class Server
{
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("args")]
    public List<string>? Arguments { get; set; }

    [JsonPropertyName("workDir")]
    public string? WorkingDirectory { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string>? Environment { get; set; }

    [JsonPropertyName("envFile")]
    public string? EnvironmentFile { get; set; }

    [JsonPropertyName("shutdownTimeout")]
    public int? ShutdownTimeout { get; set; }

    [JsonPropertyName("url")]
    public Uri? Url { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("connectionTimeout")]
    public int? ConnectionTimeout { get; set; }

    [JsonPropertyName("maxReconnectAttempts")]
    public int? MaxReconnectAttempts { get; set; }

    [JsonPropertyName("reconnectDelay")]
    public int? ReconnectDelay { get; set; }

    public McpServerConfig ToMcpServerConfig(string serverName) => Url switch
    {
        null => ToMcpStdIoServerConfig(serverName),
        _ => ToMcpSseServerConfig(serverName)
    };

    private McpServerConfig ToMcpStdIoServerConfig(string serverName)
    {
        if (Url is not null || Headers is not null || ConnectionTimeout is not null || MaxReconnectAttempts is not null || ReconnectDelay is not null)
            throw new InvalidOperationException("Server with command does not support URL, Headers, ConnectionTimeout, MaxReconnectAttempts, or ReconnectDelay");

        if (string.IsNullOrWhiteSpace(Command))
            throw new InvalidOperationException("command is empty");

        var config = new McpServerConfig
        {
            Id = serverName,
            Name = serverName,
            Location = Command,
            TransportType = TransportTypes.StdIo,
            TransportOptions = new(StringComparer.Ordinal)
            {
                ["command"] = Command,
                ["arguments"] = string.Join(' ', Arguments ?? [])
            }
        };

        if (WorkingDirectory is { } workingDirectory)
            config.TransportOptions["workingDirectory"] = workingDirectory;

        if (Environment is { } environment)
            foreach (var variable in environment)
                config.TransportOptions["env:" + variable.Key] = variable.Value;

        if (EnvironmentFile is { } environmentFile)
            foreach (var variable in DotEnv.Parse(File.ReadAllText(environmentFile)))
                config.TransportOptions["env:" + variable.Key] = variable.Value;

        if (ShutdownTimeout is { } shutdownTimeout)
            config.TransportOptions["shutdownTimeout"] = TimeSpan.FromSeconds(shutdownTimeout).ToString();

        return config;
    }

    private McpServerConfig ToMcpSseServerConfig(string serverName)
    {
        if (Command is not null || Arguments is not null || WorkingDirectory is not null || Environment is not null || EnvironmentFile is not null || ShutdownTimeout is not null)
            throw new InvalidOperationException("Server with URL does not support Command, Arguments, WorkingDirectory, Environment, EnvironmentFile or ShutdownTimeout");

        var config = new McpServerConfig
        {
            Id = serverName,
            Name = serverName,
            Location = Url?.ToString() ?? throw new InvalidOperationException("url is empty"),
            TransportType = TransportTypes.Sse,
            TransportOptions = new(StringComparer.Ordinal)
            {
                ["url"] = Url.ToString()
            }
        };

        if (Headers is { } headers)
            foreach (var header in headers)
                config.TransportOptions["header." + header.Key] = header.Value;

        if (ConnectionTimeout is { } connectionTimeout)
            config.TransportOptions["connectionTimeout"] = connectionTimeout.ToString(CultureInfo.InvariantCulture);

        if (MaxReconnectAttempts is { } maxReconnectAttempts)
            config.TransportOptions["maxReconnectAttempts"] = maxReconnectAttempts.ToString(CultureInfo.InvariantCulture);

        if (ReconnectDelay is { } reconnectDelay)
            config.TransportOptions["reconnectDelay"] = reconnectDelay.ToString(CultureInfo.InvariantCulture);

        return config;
    }
}