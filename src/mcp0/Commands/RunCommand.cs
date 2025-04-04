using System.CommandLine;
using System.Text.Json;
using ModelContextProtocol.Client;

internal sealed class RunCommand : Command
{
    public RunCommand() : base("run", "Run one or more contexts as MCP server")
    {
        var filesArgument = new Argument<string[]>
            ("contexts", "A list of context names and/or files to run");

        AddArgument(filesArgument);

        this.SetHandler(Execute, filesArgument);
    }

    private static Task Execute(string[] contexts) => Execute(contexts, CancellationToken.None);

    public static async Task Execute(string[] contexts, CancellationToken cancellationToken)
    {
        var config = new ContextConfig();

        foreach (var context in contexts)
        {
            ContextConfig? contextConfig;
            using (var stream = new FileStream(context, FileMode.Open, FileAccess.Read))
                contextConfig = await JsonSerializer.DeserializeAsync<ContextConfig>(stream, (JsonSerializerOptions?)null, cancellationToken);

            if (contextConfig is null)
                throw new InvalidOperationException("context is empty");

            config.Merge(contextConfig);
        }

        if (config.Servers is null)
            throw new InvalidOperationException("missing context servers configuration");

        var clientTasks = new List<Task<IMcpClient>>();
        foreach (var entry in config.Servers)
        {
            clientTasks.Add(McpClientFactory.CreateAsync(
                entry.Value.ToMcp(entry.Key),
                cancellationToken: cancellationToken));
        }

        var clients = await Task.WhenAll(clientTasks);
        var server = new Server("mcp0", "1.0.0");

        await server.Initialize(clients, cancellationToken);
        await server.Serve(cancellationToken);
    }
}
