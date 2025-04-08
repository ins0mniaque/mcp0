using System.CommandLine;
using mcp0.Configuration;
using mcp0.Core;

namespace mcp0.Commands;

internal sealed class InspectCommand : Command
{
    public InspectCommand() : base("inspect", "Inspect the MCP server for one or more contexts")
    {
        var contextsArgument = new Argument<string[]>("contexts", "A list of context names and/or context files to inspect")
        {
            Arity = ArgumentArity.OneOrMore
        };

        AddArgument(contextsArgument);
        AddAlias("i");

        this.SetHandler(Execute, contextsArgument);
    }

    private static Task Execute(string[] contexts) => Execute(contexts, CancellationToken.None);

    public static async Task Execute(string[] contexts, CancellationToken cancellationToken)
    {
        using var loggerFactory = Log.CreateLoggerFactory();

        var config = await ContextConfig.Read(contexts, cancellationToken);

        var servers = config.Servers?.Select(static entry => entry.Value.ToMcpServerConfig(entry.Key)).ToList() ?? [];
        var clients = await servers.CreateMcpClientsAsync(loggerFactory, cancellationToken);

        var server = new Server(Server.NameFrom(servers.Select(static server => server.Name)), Server.Version, loggerFactory);

        await server.Initialize(clients, cancellationToken);

        const ConsoleColor SectionColor = ConsoleColor.Magenta;
        const ConsoleColor HeaderColor = ConsoleColor.Green;
        const ConsoleColor ErrorColor = ConsoleColor.Red;
        const string Indentation = "  ";

        var width = Terminal.Width;

        foreach (var client in clients)
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

        if (server.Prompts.Count is not 0)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Prompts", SectionColor);

            foreach (var entry in server.Prompts)
            {
                var prompt = entry.Value.Prompt;

                Terminal.Write(Indentation);
                Terminal.Write(prompt.Name, HeaderColor);
                Terminal.Write(": ");
                WriteDescription(prompt.Description, width);
            }
        }

        if (server.Resources.Count is not 0)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Resources", SectionColor);

            foreach (var entry in server.Resources)
            {
                var resource = entry.Value.Resource;

                Terminal.Write(Indentation);
                Terminal.Write(resource.Name, HeaderColor);
                Terminal.Write(": ");
                WriteDescription(resource.Description, width);
            }
        }

        if (server.ResourceTemplates.Count is not 0)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Resource Templates", SectionColor);

            foreach (var entry in server.ResourceTemplates)
            {
                var resourceTemplate = entry.Value.ResourceTemplate;

                Terminal.Write(Indentation);
                Terminal.Write(resourceTemplate.Name, HeaderColor);
                Terminal.Write(": ");
                WriteDescription(resourceTemplate.Description, width);
            }
        }

        if (server.Tools.Count is not 0)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Tools", SectionColor);

            foreach (var entry in server.Tools)
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

    private static void WriteJsonSchema(JsonSchemaNode node, bool asArguments = false)
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

        if (node is JsonSchemaType { IsRequired: false })
            Terminal.Write("?", DecoratorColor);
    }
}
