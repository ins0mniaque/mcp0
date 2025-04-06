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
        var config = await Context.Load(contexts, cancellationToken);

        using var loggerFactory = Log.CreateLoggerFactory();

        var client = new Client(loggerFactory);
        var clientServers = config.Servers?.Select(entry => entry.Value.ToMcp(entry.Key)).ToList() ?? [];

        await client.Initialize(clientServers, cancellationToken);

        var name = string.Join('/', clientServers.Select(entry => entry.Name).DefaultIfEmpty("mcp0"));
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
        var server = new Server(name, version, loggerFactory);

        await server.Initialize(client.Clients, cancellationToken);

        Console.WriteLine("Prompts:");

        foreach (var entry in server.Prompts)
        {
            var prompt = entry.Value.Prompt;

            Console.WriteLine($"  {prompt.Name}: ${prompt.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("Resources:");

        foreach (var entry in server.Resources)
        {
            var resource = entry.Value.Resource;

            Console.WriteLine($"  {resource.Name}: ${resource.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("Resource Templates:");

        foreach (var entry in server.ResourceTemplates)
        {
            var resourceTemplate = entry.Value.ResourceTemplate;

            Console.WriteLine($"  {resourceTemplate.Name}: ${resourceTemplate.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("Tools:");

        foreach (var entry in server.Tools)
        {
            var tool = entry.Value.Tool;

            Console.WriteLine($"  {tool.Name}({McpToolInputSchema.GetSignature(tool.ProtocolTool.InputSchema)}): ${tool.Description}");
        }
    }
}
