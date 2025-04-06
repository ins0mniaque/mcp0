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
        using var loggerFactory = Log.CreateLoggerFactory();

        var config = await ContextConfig.Read(contexts, cancellationToken);

        var servers = config.Servers?.Select(static entry => entry.Value.ToMcpServerConfig(entry.Key)).ToList() ?? [];
        var clients = await servers.CreateMcpClientsAsync(loggerFactory, cancellationToken);

        var server = new Server(Server.NameFrom(servers.Select(static server => server.Name)), Server.Version, loggerFactory);

        await server.Initialize(clients, cancellationToken);
        await server.Serve(cancellationToken);
    }
}
