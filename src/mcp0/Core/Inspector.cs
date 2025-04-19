using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

using mcp0.Mcp;

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

        var columns = Terminal.Columns;

        Terminal.WriteLine("Servers", SectionColor);

        foreach (var client in proxy.Clients)
        {
            Terminal.Write(Indentation);
            Terminal.Write(client.ServerInfo.Name, HeaderColor);
            Terminal.Write(" ");
            Terminal.Write(client.ServerInfo.Version);
            WriteItemCounts(proxy, client);
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
                WriteDescription(prompt.Description, columns);
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
                WriteDescription(resource.Description, columns);
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
                WriteDescription(resourceTemplate.Description, columns);
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
                WriteJsonSchema(JsonSchema.Parse(tool.InputSchema), asArguments: true);
                Terminal.Write(")");
                WriteDescription(tool.Description, columns);
            }
        }
    }

    private static void WriteItemCounts(McpProxy proxy, IMcpClient client)
    {
        var prompts = proxy.Prompts[client].Count();
        var resources = proxy.Resources[client].Count();
        var resourceTemplates = proxy.ResourceTemplates[client].Count();
        var tools = proxy.Tools[client].Count();

        Terminal.Write($"{ConsoleColor.DarkGray}  # ");

        var index = 0;
        WriteItemCount(prompts, "prompt", "prompts", ref index);
        WriteItemCount(resources, "resource", "resources", ref index);
        WriteItemCount(resourceTemplates, "resource template", "resource templates", ref index);
        WriteItemCount(tools, "tool", "tools", ref index);

        if (prompts is 0 && resources is 0 && resourceTemplates is 0 && tools is 0)
            Terminal.Write("No prompts, resources or tools");

        Terminal.WriteLine($"{Terminal.DefaultColor}");

        static void WriteItemCount(int count, string item, string items, ref int index)
        {
            if (count is 0)
                return;

            Terminal.Write(index++ is 0 ? string.Empty : ", ");
            Terminal.Write($"{count} {(count is 1 ? item : items)}");
        }
    }

    private static void WriteDescription(string? description, int columns)
    {
        if (columns > 16 && description?.Length > columns / 2)
        {
            Terminal.WriteLine();
            Terminal.WriteLine(Terminal.WordWrap(Buffer, description, columns - 4, 4), ConsoleColor.DarkGray);
        }
        else if (description?.Length > 0)
            Terminal.WriteLine($"{ConsoleColor.DarkGray}  # {description}{Terminal.DefaultColor}");
        else
            Terminal.WriteLine();
    }

    private static void WritePromptArguments(Prompt prompt)
    {
        if (prompt.Arguments is not { } arguments)
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
        if (proxy.Tools.TryFind(function, out var client, out var tool))
            await CallTool(proxy, client, tool, arguments, cancellationToken);
        else if (proxy.Prompts.TryFind(function, out client, out var prompt))
            await CallPrompt(proxy, client, prompt, arguments, cancellationToken);
        else
            Terminal.WriteLine($"Could not find a tool or prompt named: {function}");
    }

    private static async Task CallPrompt(McpProxy proxy, IMcpClient client, Prompt prompt, JsonElement[] arguments, CancellationToken cancellationToken)
    {
        var promptArguments = arguments.ToNamedArguments(prompt.Arguments?.Select(static argument => argument.Name));
        var promptResult = await client.GetPromptAsync(proxy.Map(prompt), promptArguments, cancellationToken: cancellationToken);
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

    private static async Task CallTool(McpProxy proxy, IMcpClient client, Tool tool, JsonElement[] arguments, CancellationToken cancellationToken)
    {
        var schemaNode = JsonSchema.Parse(tool.InputSchema);
        if (schemaNode is not JsonSchemaObjectType objectType)
        {
            Terminal.Write("Error: ", ConsoleColor.Red);
            Terminal.Write("Invalid tool input schema");
            return;
        }

        var toolArguments = arguments.ToNamedArguments(objectType.Properties.Select(static property => property.Name));
        var toolResponse = await client.CallToolAsync(proxy.Map(tool), toolArguments, cancellationToken: cancellationToken);
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
        return names?.Take(arguments.Length)
                     .Select((name, index) => KeyValuePair.Create(name, (object?)arguments[index]))
                     .ToDictionary(StringComparer.Ordinal);
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "False positive")]
    public static async Task Read(McpProxy proxy, string uri, CancellationToken cancellationToken)
    {
        if (proxy.Resources.TryFind(uri, out var client, out var resource))
        {
            var result = await client.ReadResourceAsync(proxy.Map(resource), cancellationToken);
            foreach (var content in result.Contents)
                WriteResourceContents(content);
        }
        else if (proxy.ResourceTemplates.TryMatch(uri, out client, out var resourceTemplate))
        {
            var result = await client.ReadResourceAsync(proxy.Map(resourceTemplate, uri), cancellationToken);
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