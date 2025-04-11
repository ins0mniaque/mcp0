using System.CommandLine;

using mcp0.Core;
using mcp0.Mcp;
using mcp0.Models;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Commands;

internal sealed class InspectCommand : Command
{
    public InspectCommand() : base("inspect", "Inspect the MCP server for one or more configured contexts")
    {
        var pathsArgument = new Argument<string[]>("files", "A list of context configuration files to inspect")
        {
            Arity = ArgumentArity.OneOrMore
        };

        AddAlias("i");
        AddArgument(pathsArgument);

        this.SetHandler(Execute, pathsArgument);
    }

    private static Task Execute(string[] paths) => Execute(paths, CancellationToken.None);

    private static async Task Execute(string[] paths, CancellationToken cancellationToken)
    {
        var proxyOptions = new McpProxyOptions
        {
            LoggingLevel = Log.Level?.ToLoggingLevel(),
            SetLoggingLevelCallback = static level => Log.Level = level.ToLogLevel()
        };

        Log.Level ??= LogLevel.Warning;

        using var loggerFactory = Log.CreateLoggerFactory();

        var configuration = await Model.Load(paths, cancellationToken);
        var serverOptions = configuration.ToMcpServerOptions();
        var serverName = proxyOptions.ServerInfo?.Name ??
                         serverOptions?.ServerInfo?.Name ??
                         ServerInfo.Default.Name;

        await using var transport = serverOptions is null ? null : new ClientServerTransport(serverName, loggerFactory);
        await using var serverTask = serverOptions is null ? null : new DisposableTask(async ct =>
        {
            // ReSharper disable once AccessToDisposedClosure
            await using var server = McpServerFactory.Create(transport!.ServerTransport, serverOptions);

            await server.RunAsync(ct);
        }, cancellationToken);

        var clientTransports = configuration.ToClientTransports();
        if (transport?.ClientTransport is { } clientTransport)
            clientTransports = clientTransports.Append(clientTransport).ToArray();

        proxyOptions.ServerInfo = ServerInfo.Create(clientTransports);

        await using var proxy = new McpProxy(proxyOptions, loggerFactory);

        var clients = await clientTransports.CreateMcpClientsAsync(proxy.GetClientOptions(), loggerFactory, cancellationToken);

        await proxy.ConnectAsync(clients, cancellationToken);

        Inspect(proxy);
    }

    private static void Inspect(McpProxy proxy)
    {
        const ConsoleColor SectionColor = ConsoleColor.Magenta;
        const ConsoleColor HeaderColor = ConsoleColor.Green;
        const ConsoleColor ErrorColor = ConsoleColor.Red;
        const string Indentation = "  ";

        var width = Terminal.Columns;

        foreach (var client in proxy.Clients)
        {
            if (client.ServerInfo is not { } info)
            {
                Terminal.Write("server", ErrorColor);
                Terminal.WriteLine(" (no information)");
                continue;
            }

            Terminal.Write(info.Name, HeaderColor);
            Terminal.Write(" ");
            Terminal.WriteLine(info.Version);
        }

        if (proxy.Prompts.Count is not 0)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Prompts", SectionColor);

            foreach (var entry in proxy.Prompts)
            {
                var prompt = entry.Value.Prompt;

                Terminal.Write(Indentation);
                Terminal.Write(prompt.Name, HeaderColor);
                WritePromptArguments(prompt);
                Terminal.Write(": ");
                WriteDescription(prompt.Description, width);
            }
        }

        if (proxy.Resources.Count is not 0)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Resources", SectionColor);

            foreach (var entry in proxy.Resources)
            {
                var resource = entry.Value.Resource;

                Terminal.Write(Indentation);
                Terminal.Write(resource.Name, HeaderColor);
                Terminal.Write(": ");
                WriteDescription(resource.Description, width);
            }
        }

        if (proxy.ResourceTemplates.Count is not 0)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Resource Templates", SectionColor);

            foreach (var entry in proxy.ResourceTemplates)
            {
                var resourceTemplate = entry.Value.ResourceTemplate;

                Terminal.Write(Indentation);
                Terminal.Write(resourceTemplate.Name, HeaderColor);
                Terminal.Write(": ");
                WriteDescription(resourceTemplate.Description, width);
            }
        }

        if (proxy.Tools.Count is not 0)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Tools", SectionColor);

            foreach (var entry in proxy.Tools)
            {
                var tool = entry.Value.Tool;

                Terminal.Write(Indentation);
                Terminal.Write(tool.Name, HeaderColor);
                Terminal.Write("(");
                WriteJsonSchema(JsonSchema.Parse(tool.ProtocolTool.InputSchema), asArguments: true);
                Terminal.Write("): ");
                WriteDescription(tool.Description, width);
            }
        }
    }

    private static void WritePromptArguments(McpClientPrompt prompt)
    {
        if (prompt.ProtocolPrompt.Arguments is not { } arguments)
            return;

        var properties = arguments.Select(ToJsonSchema).ToArray();
        var objectType = new JsonSchemaObjectType(properties, true);

        Terminal.Write("(");
        WriteJsonSchema(objectType, asArguments: true);
        Terminal.Write(")");

        static JsonSchemaProperty ToJsonSchema(PromptArgument argument)
        {
            return new(argument.Name, new JsonSchemaPrimitiveType("string", argument.Required is true));
        }
    }

    private static void WriteDescription(string? description, int width)
    {
        if (width > 16 && description?.Length > width / 2)
        {
            Terminal.WriteLine();
            Terminal.WriteLine(Terminal.Wrap(description, width - 4, 4));
        }
        else
            Terminal.WriteLine(description);
    }

    private static void WriteJsonSchema(IJsonSchemaNode node, bool asArguments = false)
    {
        const ConsoleColor PropertyNameColor = ConsoleColor.Blue;
        const ConsoleColor PrimitiveTypeColor = ConsoleColor.Magenta;
        const ConsoleColor SymbolColor = ConsoleColor.Cyan;
        const ConsoleColor DecoratorColor = ConsoleColor.DarkYellow;

        if (node is JsonSchemaObjectType objectType)
        {
            Terminal.Write(asArguments ? string.Empty : "{", DecoratorColor);

            var separator = string.Empty;
            foreach (var property in objectType.Properties)
            {
                Terminal.Write(separator);
                separator = ", ";

                Terminal.Write(property.Name, PropertyNameColor);
                if (property.Type is JsonSchemaType { Required: false })
                    Terminal.Write("?", DecoratorColor);

                Terminal.Write(":");
                WriteJsonSchema(property.Type);
            }

            Terminal.Write(asArguments ? string.Empty : "}", DecoratorColor);
        }
        else if (node is JsonSchemaArrayType arrayType)
        {
            WriteJsonSchema(arrayType.ElementType);
            Terminal.Write("[]", DecoratorColor);
        }
        else if (node is JsonSchemaUnionType union)
        {
            var separator = string.Empty;
            foreach (var unionType in union.UnionTypes)
            {
                Terminal.Write(separator, DecoratorColor);
                separator = "|";

                WriteJsonSchema(unionType);
            }
        }
        else if (node is JsonSchemaPrimitiveType primitiveType)
            Terminal.Write(primitiveType.Name, PrimitiveTypeColor);
        else if (node is JsonSchemaSymbol symbol)
            Terminal.Write(symbol.Name, SymbolColor);
        else
            throw new ArgumentException($"Unknown JSON schema node: {node.GetType().Name}", nameof(node));
    }
}