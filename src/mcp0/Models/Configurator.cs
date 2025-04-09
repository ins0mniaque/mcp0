using mcp0.Core;

using ModelContextProtocol.Protocol.Transport;

namespace mcp0.Models;

internal static class Configurator
{
    public static IClientTransport[] ToClientTransports(this Configuration configuration)
    {
        return configuration.Servers?.Select(static entry => entry.Value.ToClientTransport(entry.Key)).ToArray() ?? [];
    }

    public static IClientTransport ToClientTransport(this Server server, string serverName) => server.Url switch
    {
        null => server.ToStdioClientTransport(serverName),
        _ => server.ToSseClientTransport(serverName)
    };

    private static StdioClientTransport ToStdioClientTransport(this Server server, string serverName)
    {
        if (server.Url is not null || server.Headers is not null || server.ConnectionTimeout is not null || server.MaxReconnectAttempts is not null || server.ReconnectDelay is not null)
            throw new InvalidOperationException("Server with command does not support URL, Headers, ConnectionTimeout, MaxReconnectAttempts, or ReconnectDelay");

        if (string.IsNullOrWhiteSpace(server.Command))
            throw new InvalidOperationException("Command is empty");

        var environment = server.Environment?.ToDictionary();
        if (server.EnvironmentFile is { } environmentFile)
        {
            environment ??= new Dictionary<string, string>();
            foreach (var variable in DotEnv.Parse(File.ReadAllText(environmentFile)))
                environment[variable.Key] = variable.Value;
        }

        return new StdioClientTransport(new()
        {
            Name = serverName,
            Command = server.Command,
            Arguments = server.Arguments,
            WorkingDirectory = server.WorkingDirectory,
            EnvironmentVariables = environment?.Count is 0 ? null : environment,
            ShutdownTimeout = server.ShutdownTimeout?.ToTimeSpan() ?? StdioClientTransportOptions.DefaultShutdownTimeout,
        });
    }

    private static readonly SseClientTransportOptions defaultSseClientTransportOptions = new()
    {
        Endpoint = new Uri("http://localhost:8080")
    };

    private static SseClientTransport ToSseClientTransport(this Server server, string serverName)
    {
        if (server.Command is not null || server.Arguments is not null || server.WorkingDirectory is not null || server.Environment is not null || server.EnvironmentFile is not null || server.ShutdownTimeout is not null)
            throw new InvalidOperationException("Server with URL does not support Command, Arguments, WorkingDirectory, Environment, EnvironmentFile or ShutdownTimeout");

        if (server.Url is null)
            throw new InvalidOperationException("URL is empty");

        return new SseClientTransport(new()
        {
            Name = serverName,
            Endpoint = server.Url,
            AdditionalHeaders = server.Headers,
            ConnectionTimeout = server.ConnectionTimeout?.ToTimeSpan() ?? defaultSseClientTransportOptions.ConnectionTimeout,
            MaxReconnectAttempts = server.MaxReconnectAttempts ?? defaultSseClientTransportOptions.MaxReconnectAttempts,
            ReconnectDelay = server.ReconnectDelay?.ToTimeSpan() ?? defaultSseClientTransportOptions.ReconnectDelay
        });
    }

    private static TimeSpan ToTimeSpan(this int seconds) => TimeSpan.FromSeconds(seconds);
}