using System.CommandLine;

internal sealed class RunCommand : Command
{
    public RunCommand() : base("run", "Run one or more contexts as an MCP server")
    {
        var contextsArgument = new Argument<string[]>("contexts", "A list of context names and/or context files to run")
        {
            Arity = ArgumentArity.OneOrMore
        };

        AddArgument(contextsArgument);

        this.SetHandler(Execute, contextsArgument);
    }

    private static Task Execute(string[] contexts) => Execute(contexts, CancellationToken.None);

    public static async Task Execute(string[] contexts, CancellationToken cancellationToken)
    {
        var config = await ContextConfig.Read(contexts, cancellationToken);

        using var loggerFactory = Log.CreateLoggerFactory();

        var clientServers = config.Servers?.Select(entry => entry.Value.ToMcpServerConfig(entry.Key)).ToList() ?? [];
        var clients = await clientServers.CreateMcpClientsAsync(loggerFactory, cancellationToken);

        var name = string.Join('/', clientServers.Select(entry => entry.Name).DefaultIfEmpty("mcp0"));
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
        var server = new Server(name, version, loggerFactory);

        await server.Initialize(clients, cancellationToken);
        await server.Serve(cancellationToken);
    }
}
