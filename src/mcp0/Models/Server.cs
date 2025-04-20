using System.Text.Json.Serialization;

using Generator.Equals;

using mcp0.Core;

namespace mcp0.Models;

[JsonConverter(typeof(ServerConverter))]
internal abstract record Server
{
    public string? Name { get; set; }

    public static Server Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid server: {text}");
    }

    public static Server? TryParse(string text)
    {
        return (Server?)SseServer.TryParse(text) ?? StdioServer.TryParse(text);
    }
}

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

    public static new StdioServer? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);

        CommandLine.Split(text, out var command, out var arguments, environment);

        if (command is null)
            return null;

        return new()
        {
            Command = command,
            Arguments = arguments,
            Environment = environment.Count is 0 ? null : environment
        };
    }
}

[Equatable]
internal sealed partial record SseServer : Server
{
    public required Uri Url { get; init; }

    [UnorderedEquality]
    public Dictionary<string, string>? Headers { get; init; }
    public TimeSpan? ConnectionTimeout { get; init; }

    public static new SseServer? TryParse(string text)
    {
        if (!Uri.IsWellFormedUriString(text, UriKind.Absolute))
            return null;

        return new() { Url = new Uri(text) };
    }
}