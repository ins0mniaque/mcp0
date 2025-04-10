using System.Collections.Immutable;
using System.Text.Json;

using mcp0.Core;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Models;

internal static class Configurator
{
    public static McpServerOptions? ToMcpServerOptions(this Configuration configuration)
    {
        var prompts = configuration.ToPromptsCapability();
        var resources = configuration.ToResourcesCapability();
        var tools = configuration.ToToolsCapability();
        if (prompts is null && resources is null && tools is null)
            return null;

        return new()
        {
            ServerInfo = new() { Name = McpProxy.Name, Version = McpProxy.Version },
            Capabilities = new()
            {
                Prompts = prompts,
                Resources = resources,
                Tools = tools
            }
        };
    }

    private static PromptsCapability? ToPromptsCapability(this Configuration configuration)
    {
        if (configuration.Prompts is null)
            return null;

        var prompts = configuration.Prompts.ToDictionary(e => e.Key, e =>
        {
            var arguments = PromptTemplate.Parse(e.Value);
            var prompt = new Prompt { Name = e.Key, Description = null, Arguments = arguments.Count is 0 ? null : arguments };

            return (Prompt: prompt, Template: e.Value);
        });

        var listPromptsResultTask = Task.FromResult(new ListPromptsResult
        {
            Prompts = prompts.Select(entry => entry.Value.Prompt).ToList()
        });

        return new()
        {
            ListPromptsHandler = (_, _) => listPromptsResultTask,
            GetPromptHandler = async (request, _) =>
            {
                if (request.Params?.Name is not { } name || !prompts.TryGetValue(name, out var prompt))
                    throw new McpException($"Unknown prompt: {request.Params?.Name}");

                var text = prompt.Template;
                if (request.Params?.Arguments is { } arguments)
                    text = Template.Render(text, arguments);

                return await Task.FromResult(new GetPromptResult
                {
                    Messages = [new() { Role = Role.User, Content = new Content { Type = "text", Text = text } }]
                });
            }
        };
    }

    private static ResourcesCapability? ToResourcesCapability(this Configuration configuration)
    {
        if (configuration.Resources is null)
            return null;

        var resources = configuration.Resources.ToDictionary(e => new Uri(e.Value).ToString(), e => new Resource
        {
            Name = e.Key,
            Uri = new Uri(e.Value).ToString(),
            MimeType = MimeType.FromExtension(Path.GetExtension(e.Value))
        });

        var listResourcesResultTask = Task.FromResult(new ListResourcesResult
        {
            Resources = resources.Select(entry => entry.Value).ToList()
        });

        return new()
        {
            ListResourcesHandler = (_, _) => listResourcesResultTask,
            ReadResourceHandler = async (request, cancellationToken) =>
            {
                if (request.Params?.Uri is not { } uri || !resources.TryGetValue(uri, out var resource))
                    throw new McpException($"Unknown resource: {request.Params?.Uri}");

                var path = new Uri(resource.Uri).LocalPath;
                var data = await File.ReadAllBytesAsync(path, cancellationToken);
                var contents = await resource.ToResourceContents(data, cancellationToken);

                return new() { Contents = [contents] };
            }
        };
    }

    private static ToolsCapability? ToToolsCapability(this Configuration configuration)
    {
        if (configuration.Tools is null)
            return null;

        var tools = configuration.Tools.ToDictionary(e => e.Key, e =>
        {
            var tool = new Tool { Name = e.Key, InputSchema = ToolTemplate.Parse(e.Value) };

            return (Tool: tool, Template: e.Value);
        });

        var listToolsResultTask = Task.FromResult(new ListToolsResult
        {
            Tools = tools.Select(entry => entry.Value.Tool).ToList()
        });

        return new()
        {
            ListToolsHandler = (_, _) => listToolsResultTask,
            CallToolHandler = async (request, cancellationToken) =>
            {
                if (request.Params?.Name is not { } name || !tools.TryGetValue(name, out var tool))
                    throw new McpException($"Unknown tool: {request.Params?.Name}");

                var arguments = request.Params?.Arguments ?? ImmutableDictionary<string, JsonElement>.Empty;
                var commandLine = ToolCommand.Parse(tool.Template, arguments);
                var (stdout, stderr, exitCode) = await ToolCommand.Run(commandLine, cancellationToken);

                var output = stdout.Trim();
                if (exitCode is not 0 && !string.IsNullOrWhiteSpace(stderr))
                    output += $"\n\nError: {stderr.Trim()}";

                var content = new Content { Type = "text", Text = output };

                return new CallToolResponse { Content = [content], IsError = exitCode is not 0 };
            }
        };
    }

    private static async Task<ResourceContents> ToResourceContents(this Resource resource, byte[] data, CancellationToken cancellationToken)
    {
        var binary = data.AsSpan().Contains((byte)0);
        if (binary)
            return new BlobResourceContents
            {
                Uri = resource.Uri,
                Blob = Convert.ToBase64String(data),
                MimeType = resource.MimeType
            };

        string text;
        using (var stream = new MemoryStream(data))
        using (var reader = new StreamReader(stream))
            text = await reader.ReadToEndAsync(cancellationToken);

        return new TextResourceContents
        {
            Uri = resource.Uri,
            Text = text,
            MimeType = resource.MimeType
        };
    }

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