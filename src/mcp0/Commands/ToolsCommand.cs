using System.CommandLine;

internal sealed class ToolsCommand : Command
{
    public ToolsCommand() : base("tools", "List tools for one or more contexts")
    {
        var contextsArgument = new Argument<string[]>("contexts", "A list of context names and/or context files to list tools from");

        AddArgument(contextsArgument);

        this.SetHandler(Execute, contextsArgument);
    }

    private static Task Execute(string[] contexts) => Execute(contexts, CancellationToken.None);

    public static async Task Execute(string[] contexts, CancellationToken cancellationToken)
    {
        var config = await Context.Load(contexts, cancellationToken);

        using var loggerFactory = Logging.CreateLoggerFactory();

        var client = new Client(loggerFactory);
        var clientServers = config.Servers?.Select(entry => entry.Value.ToMcp(entry.Key));

        await client.Initialize(clientServers ?? [], cancellationToken);

        var server = new Server("mcp0", "1.0.0", loggerFactory);

        await server.Initialize(client.Clients, cancellationToken);

        foreach (var entry in server.Tools)
        {
            var tool = entry.Value.Tool;

            Console.WriteLine($"{tool.Name}: ${tool.Description}");
            Console.WriteLine($"  Input Schema: {tool.ProtocolTool.InputSchema}");
        }
    }
}
