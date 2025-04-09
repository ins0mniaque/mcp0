using System.Globalization;

using mcp0.Core;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Transport;

namespace mcp0.Models;

internal static class Configurator
{
    public static McpServerConfig[] ToMcpServerConfigs(this Configuration configuration)
    {
        return configuration.Servers?.Select(static entry => entry.Value.ToMcpServerConfig(entry.Key)).ToArray() ?? [];
    }

    public static McpServerConfig ToMcpServerConfig(this Server server, string serverName) => server.Url switch
    {
        null => server.ToMcpStdIoServerConfig(serverName),
        _ => server.ToMcpSseServerConfig(serverName)
    };

    private static McpServerConfig ToMcpStdIoServerConfig(this Server server, string serverName)
    {
        if (server.Url is not null || server.Headers is not null || server.ConnectionTimeout is not null || server.MaxReconnectAttempts is not null || server.ReconnectDelay is not null)
            throw new InvalidOperationException("Server with command does not support URL, Headers, ConnectionTimeout, MaxReconnectAttempts, or ReconnectDelay");

        if (string.IsNullOrWhiteSpace(server.Command))
            throw new InvalidOperationException("Command is empty");

        var config = new McpServerConfig
        {
            Id = serverName,
            Name = serverName,
            Location = server.Command,
            TransportType = TransportTypes.StdIo,
            TransportOptions = new(StringComparer.Ordinal)
            {
                ["command"] = server.Command,
                ["arguments"] = string.Join(' ', server.Arguments ?? [])
            }
        };

        if (server.WorkingDirectory is { } workingDirectory)
            config.TransportOptions["workingDirectory"] = workingDirectory;

        if (server.Environment is { } environment)
            foreach (var variable in environment)
                config.TransportOptions["env:" + variable.Key] = variable.Value;

        if (server.EnvironmentFile is { } environmentFile)
            foreach (var variable in DotEnv.Parse(File.ReadAllText(environmentFile)))
                config.TransportOptions["env:" + variable.Key] = variable.Value;

        if (server.ShutdownTimeout is { } shutdownTimeout)
            config.TransportOptions["shutdownTimeout"] = TimeSpan.FromSeconds(shutdownTimeout).ToString();

        return config;
    }

    private static McpServerConfig ToMcpSseServerConfig(this Server server, string serverName)
    {
        if (server.Command is not null || server.Arguments is not null || server.WorkingDirectory is not null || server.Environment is not null || server.EnvironmentFile is not null || server.ShutdownTimeout is not null)
            throw new InvalidOperationException("Server with URL does not support Command, Arguments, WorkingDirectory, Environment, EnvironmentFile or ShutdownTimeout");

        var config = new McpServerConfig
        {
            Id = serverName,
            Name = serverName,
            Location = server.Url?.ToString() ?? throw new InvalidOperationException("URL is empty"),
            TransportType = TransportTypes.Sse,
            TransportOptions = new(StringComparer.Ordinal)
            {
                ["url"] = server.Url.ToString()
            }
        };

        if (server.Headers is { } headers)
            foreach (var header in headers)
                config.TransportOptions["header." + header.Key] = header.Value;

        if (server.ConnectionTimeout is { } connectionTimeout)
            config.TransportOptions["connectionTimeout"] = connectionTimeout.ToString(CultureInfo.InvariantCulture);

        if (server.MaxReconnectAttempts is { } maxReconnectAttempts)
            config.TransportOptions["maxReconnectAttempts"] = maxReconnectAttempts.ToString(CultureInfo.InvariantCulture);

        if (server.ReconnectDelay is { } reconnectDelay)
            config.TransportOptions["reconnectDelay"] = reconnectDelay.ToString(CultureInfo.InvariantCulture);

        return config;
    }
}