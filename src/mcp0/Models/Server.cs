using System.Text.Json.Serialization;

using Generator.Equals;

namespace mcp0.Models;

[JsonConverter(typeof(ServerConverter))]
internal abstract record Server;

[Equatable]
internal sealed partial record StdioServer : Server
{
    public required string Command { get; init; }
    [OrderedEquality]
    public string[]? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    [UnorderedEquality]
    public Dictionary<string, string>? Environment { get; init; }
    public string? EnvironmentFile { get; init; }
    public TimeSpan? ShutdownTimeout { get; init; }
}

[Equatable]
internal sealed partial record SseServer : Server
{
    public required Uri Url { get; init; }
    [UnorderedEquality]
    public Dictionary<string, string>? Headers { get; init; }
    public TimeSpan? ConnectionTimeout { get; init; }
    public int? MaxReconnectAttempts { get; init; }
    public TimeSpan? ReconnectDelay { get; init; }
}