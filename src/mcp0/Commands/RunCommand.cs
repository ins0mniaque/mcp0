using System.CommandLine;

internal sealed class RunCommand : Command
{
    public RunCommand() : base("run", "Run one or more contexts as an MCP server")
    {
        var filesArgument = new Argument<string[]>
            ("contexts", "A list of context names and/or context files to run");

        AddArgument(filesArgument);

        this.SetHandler(Execute, filesArgument);
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
        await server.Serve(cancellationToken);
    }
}
