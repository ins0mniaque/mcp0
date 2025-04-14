using mcp0.Mcp;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal static class Inspector
{
    private static ReadOnlySpan<char> Indentation => "  ";

    public static void Inspect(McpProxy proxy)
    {
        const ConsoleColor SectionColor = ConsoleColor.Magenta;
        const ConsoleColor HeaderColor = ConsoleColor.Green;
        const ConsoleColor ErrorColor = ConsoleColor.Red;

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

            foreach (var prompt in proxy.Prompts)
            {
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

            foreach (var resource in proxy.Resources)
            {
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

            foreach (var resourceTemplate in proxy.ResourceTemplates)
            {
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

            foreach (var tool in proxy.Tools)
            {
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