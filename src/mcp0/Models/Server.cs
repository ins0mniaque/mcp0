using System.Buffers;
using System.Text.Json.Serialization;

using Generator.Equals;

using mcp0.Core;

namespace mcp0.Models;

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

    public static string? TryFormat(Server server) => server switch
    {
        StdioServer stdioServer => StdioServer.TryFormat(stdioServer),
        SseServer sseServer => SseServer.TryFormat(sseServer),
        _ => null
    };
}

[Equatable]
internal sealed partial record StdioServer : Server
{
    public required string Command { get; init; }

    [JsonPropertyName("args")]
    [OrderedEquality]
    public string[]? Arguments { get; init; }

    [JsonPropertyName("workDir")]
    public string? WorkingDirectory { get; init; }

    [JsonPropertyName("env")]
    [UnorderedEquality]
    public Dictionary<string, string>? Environment { get; init; }

    [JsonPropertyName("envFile")]
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

    private static readonly SearchValues<char> commandDelimiters = SearchValues.Create(' ', '\"', '\'');

    public static string? TryFormat(StdioServer server)
    {
        var formattable = server.Name is null &&
                          server.WorkingDirectory is null &&
                          server.Environment is null &&
                          server.EnvironmentFile is null &&
                          server.ShutdownTimeout is null &&
                          server.Command.AsSpan().ContainsAny(commandDelimiters) is false &&
                          server.Arguments?.Any(a => a.AsSpan().ContainsAny(commandDelimiters)) is false or null;

        if (!formattable)
            return null;

        if (server.Arguments is null || server.Arguments.Length is 0)
            return server.Command;

        return server.Command + ' ' + string.Join(' ', server.Arguments);
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

    public static string? TryFormat(SseServer server)
    {
        var formattable = server.Name is null &&
                          server.Headers is null &&
                          server.ConnectionTimeout is null;

        return formattable ? server.Url.ToString() : null;
    }
}