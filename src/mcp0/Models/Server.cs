using System.Text.Json.Serialization;

namespace mcp0.Models;

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
}