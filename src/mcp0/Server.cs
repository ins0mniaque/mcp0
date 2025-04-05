using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

internal sealed class Server
{
    private readonly Dictionary<string, (IMcpClient Client, McpClientTool Tool)> tools = new();
    private readonly string name;
    private readonly string version;
    private readonly ILoggerFactory loggerFactory;

    public Server(string name, string version, ILoggerFactory loggerFactory)
    {
        this.name = name;
        this.version = version;
        this.loggerFactory = loggerFactory;
    }

    // TODO: Handle tool changed event (see IMcpClient.AddNotificationHandler)
    public async Task Initialize(IReadOnlyList<IMcpClient> clients, CancellationToken cancellationToken)
    {
        tools.Clear();

        var listToolsTasks = new List<Task<IList<McpClientTool>>>();
        foreach (var client in clients)
            listToolsTasks.Add(client.ListToolsAsync(null, cancellationToken));

        var clientsTools = await Task.WhenAll(listToolsTasks);
        for (int i = 0; i < clientsTools.Length; i++)
        {
            var client = clients[i];
            var clientTools = clientsTools[i];
            foreach (var clientTool in clientTools)
                tools[clientTool.Name] = (client, clientTool);
        }
    }

    public async Task Serve(CancellationToken cancellationToken)
    {
        var listToolsResultTask = Task.FromResult(new ListToolsResult
        {
            Tools = tools.Select(entry => entry.Value.Tool.ProtocolTool).ToList()
        });

        var options = new McpServerOptions
        {
            ServerInfo = new() { Name = name, Version = version },
            Capabilities = new()
            {
                Tools = new()
                {
                    // TODO: Support ListTools pagination (see ListToolsRequestParams.Cursor).
                    ListToolsHandler = (request, cancellationToken) => listToolsResultTask,
                    CallToolHandler = async (request, cancellationToken) =>
                    {
                        if (request.Params?.Name is not { } name)
                            throw new McpServerException($"Missing tool name");

                        if (!tools.TryGetValue(name, out var tool))
                            throw new McpServerException($"Unknown tool: '{name}'");

                        var arguments = request.Params.Arguments?.ToDictionary(entry => entry.Key, entry => (object?)entry.Value);

                        return await tool.Client.CallToolAsync(name, arguments, null, cancellationToken);
                    },
                }
            },
        };

        await using var transport = new StdioServerTransport(name, loggerFactory);
        await using IMcpServer server = McpServerFactory.Create(transport, options, loggerFactory);

        await server.RunAsync(cancellationToken);
    }
}
