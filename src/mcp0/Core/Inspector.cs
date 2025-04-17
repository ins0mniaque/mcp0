using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

using mcp0.Mcp;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal static class Inspector
{
    private static StringBuilder Buffer { get; } = new(0);
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
                Terminal.Write(resource.Uri);
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
                Terminal.Write(resourceTemplate.UriTemplate);
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
                Terminal.Write(")");
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
            Terminal.WriteLine(Terminal.WordWrap(Buffer, description, width - 4, 4), ConsoleColor.DarkGray);
        }
        else if (description?.Length > 0)
            Terminal.WriteLine($"{ConsoleColor.DarkGray}  # {description}{Terminal.DefaultColor}");
        else
            Terminal.WriteLine();
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
            Terminal.Write(TypeAlias.FromJsonSchema(primitiveType.Name), PrimitiveTypeColor);
        else if (node is JsonSchemaSymbol symbol)
            Terminal.Write(symbol.Name, SymbolColor);
        else
            throw new ArgumentException($"Unknown JSON schema node: {node.GetType().Name}", nameof(node));
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "False positive")]
    public static async Task Call(McpProxy proxy, string function, JsonElement[] arguments, CancellationToken cancellationToken)
    {
        try
        {
            if (proxy.Tools.TryFind(function, out var client, out var tool))
                await CallTool(client, tool, arguments, cancellationToken);
            else if (proxy.Prompts.TryFind(function, out client, out var prompt))
                await CallPrompt(client, prompt, arguments, cancellationToken);
            else
                Terminal.WriteLine($"Could not find a tool or prompt named: {function}");
        }
        catch (McpException) { }
    }

    private static async Task CallPrompt(IMcpClient client, McpClientPrompt prompt, JsonElement[] arguments, CancellationToken cancellationToken)
    {
        var promptArguments = arguments.ToNamedArguments(prompt.ProtocolPrompt.Arguments?.Select(static argument => argument.Name));
        var promptResult = await client.GetPromptAsync(prompt.Name, promptArguments, null, cancellationToken);
        if (promptResult.Description is not null)
        {
            Terminal.Write("Description: ");
            Terminal.WriteLine(promptResult.Description);
        }

        foreach (var message in promptResult.Messages)
        {
            Terminal.Write(message.Role.ToString());
            Terminal.Write(": ");
            WriteContent(message.Content);
        }
    }

    private static async Task CallTool(IMcpClient client, McpClientTool tool, JsonElement[] arguments, CancellationToken cancellationToken)
    {
        var schemaNode = JsonSchema.Parse(tool.JsonSchema);
        if (schemaNode is not JsonSchemaObjectType objectType)
        {
            Terminal.Write("Error: ", ConsoleColor.Red);
            Terminal.Write("Invalid tool input schema");
            return;
        }

        var toolArguments = arguments.ToNamedArguments(objectType.Properties.Select(static property => property.Name));
        var toolResponse = await client.CallToolAsync(tool.Name, toolArguments, null, cancellationToken);
        if (toolResponse.IsError)
            Terminal.Write("Error: ", ConsoleColor.Red);

        foreach (var content in toolResponse.Content)
            WriteContent(content);
    }

    private static void WriteContent(Content content)
    {
        if (content.MimeType is { } mimeType)
            Terminal.WriteLine(mimeType);

        if (content.Resource is { } resource)
            Terminal.WriteLine(resource.Uri);

        if (content.Text is { } text)
            Terminal.WriteLine(text);

        if (content.Data is { } data)
            Terminal.WriteLine(data);
    }

    private static Dictionary<string, object?>? ToNamedArguments(this JsonElement[] arguments, IEnumerable<string>? names)
    {
        return names?.Select((name, index) => KeyValuePair.Create(name, (object?)arguments.ElementAtOrDefault(index)))
                     .ToDictionary(StringComparer.Ordinal);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "False positive")]
    public static async Task Read(McpProxy proxy, string uri, CancellationToken cancellationToken)
    {
        if (proxy.Resources.TryFind(uri, out var client, out _))
        {
            var result = await client.ReadResourceAsync(uri, cancellationToken);
            foreach (var content in result.Contents)
                WriteResourceContents(content);
        }
        else if (proxy.ResourceTemplates.TryMatch(uri, out client, out _))
        {
            var result = await client.ReadResourceAsync(uri, cancellationToken);
            foreach (var content in result.Contents)
                WriteResourceContents(content);
        }
        else
            Terminal.WriteLine($"Could not find resource {uri}");
    }

    private static void WriteResourceContents(ResourceContents contents)
    {
        if (contents.MimeType is not null)
            Terminal.WriteLine(contents.MimeType);

        if (contents is TextResourceContents text)
            Terminal.WriteLine(text.Text);
        else if (contents is BlobResourceContents blob)
            Terminal.WriteLine(blob.Blob);
    }
}