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
            ServerInfo = ServerInfo.Default,
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

        var prompts = configuration.Prompts.ToDictionary(
            entry => entry.Key,
            entry =>
            {
                var arguments = PromptTemplate.Parse(entry.Value);
                var prompt = new Prompt
                {
                    Name = entry.Key,
                    Description = null,
                    Arguments = arguments.Count is 0 ? null : arguments
                };

                return (Prompt: prompt, Template: entry.Value);
            },
            StringComparer.Ordinal);

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

        var resources = configuration.Resources
            .Select(entry => UriResource.Create(entry.Key, Posix.ExpandPath(entry.Value)))
            .ToDictionary(resource => resource.Uri, StringComparer.Ordinal);

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

                var (data, mimetype) = await resource.Download(cancellationToken);
                var contents = await resource.ToResourceContents(data, mimetype, cancellationToken);

                return new() { Contents = [contents] };
            }
        };
    }

    private static ToolsCapability? ToToolsCapability(this Configuration configuration)
    {
        if (configuration.Tools is null)
            return null;

        var tools = configuration.Tools.ToDictionary(
            entry => entry.Key,
            entry =>
            {
                var template = entry.Value;
                var tool = new Tool
                {
                    Name = entry.Key,
                    Description = ToolTemplate.ParseDescription(ref template),
                    InputSchema = ToolTemplate.ParseInputSchema(template)
                };

                return (Tool: tool, Template: template);
            },
            StringComparer.Ordinal);

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

    public static IClientTransport[] ToClientTransports(this Configuration configuration)
    {
        return configuration.Servers?.Select(static entry => entry.Value.ToClientTransport(entry.Key)).ToArray() ?? [];
    }

    public static IClientTransport ToClientTransport(this Server server, string serverName) => server switch
    {
        StdioServer stdioServer => new StdioClientTransport(stdioServer.ToClientTransportOptions(serverName)),
        SseServer sseServer => new SseClientTransport(sseServer.ToClientTransportOptions(serverName)),
        _ => throw new ArgumentException($"Unknown server type: {server.GetType().Name}", nameof(server))
    };

    public static StdioClientTransportOptions ToClientTransportOptions(this StdioServer server, string serverName)
    {
        var environment = server.Environment?.ToDictionary(StringComparer.Ordinal);
        if (server.EnvironmentFile is { } environmentFile)
        {
            environment ??= new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var variable in DotEnv.Parse(File.ReadAllText(Posix.ExpandPath(environmentFile))))
                environment[variable.Key] = variable.Value;
        }

        return new()
        {
            Name = serverName,
            Command = server.Command,
            Arguments = server.Arguments,
            WorkingDirectory = server.WorkingDirectory,
            EnvironmentVariables = environment?.Count is 0 ? null : environment,
            ShutdownTimeout = server.ShutdownTimeout ?? defaultStdioServer.ShutdownTimeout
        };
    }

    public static SseClientTransportOptions ToClientTransportOptions(this SseServer server, string serverName)
    {
        return new()
        {
            Name = serverName,
            Endpoint = server.Url,
            AdditionalHeaders = server.Headers,
            ConnectionTimeout = server.ConnectionTimeout ?? defaultSseServer.ConnectionTimeout,
            MaxReconnectAttempts = server.MaxReconnectAttempts ?? defaultSseServer.MaxReconnectAttempts,
            ReconnectDelay = server.ReconnectDelay ?? defaultSseServer.ReconnectDelay
        };
    }

    private static readonly StdioClientTransportOptions defaultStdioServer = new() { Command = "_" };
    private static readonly SseClientTransportOptions defaultSseServer = new() { Endpoint = new Uri("http://_/") };
}