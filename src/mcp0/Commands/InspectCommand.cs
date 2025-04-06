using System.CommandLine;

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
        const ConsoleColor ArgumentColor = ConsoleColor.Cyan;
        const ConsoleColor ErrorColor = ConsoleColor.Red;
        const string Indentation = "  ";

        foreach (var client in clients)
        {
            if (client.ServerInfo is not { } info)
            {
                Terminal.Write("server", ErrorColor);
                Terminal.WriteLine(" (no information)");
                continue;
            }

            Terminal.Write(client.ServerInfo.Name, HeaderColor);
            Terminal.Write(" ");
            Terminal.WriteLine(client.ServerInfo.Version);
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
                Terminal.WriteLine(prompt.Description);
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
                Terminal.WriteLine(resource.Description);
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
                Terminal.WriteLine(resourceTemplate.Description);
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
                Terminal.Write(McpToolInputSchema.GetSignature(tool.ProtocolTool.InputSchema), ArgumentColor);
                Terminal.Write("): ");
                Terminal.WriteLine(tool.Description);
            }
        }
    }
}
