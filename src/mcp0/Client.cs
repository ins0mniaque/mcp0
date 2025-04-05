using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;

internal sealed class Client
{
    private readonly List<IMcpClient> clients;
    private readonly ILoggerFactory loggerFactory;

    public Client(ILoggerFactory loggerFactory)
    {
        Clients = clients = new();

        this.loggerFactory = loggerFactory;
    }

    public IReadOnlyList<IMcpClient> Clients { get; }

    public async Task Initialize(IEnumerable<McpServerConfig> servers, CancellationToken cancellationToken)
    {
        clients.Clear();

        var clientTasks = new List<Task<IMcpClient>>();
        foreach (var server in servers)
        {
            clientTasks.Add(McpClientFactory.CreateAsync(
                server,
                loggerFactory: loggerFactory,
                cancellationToken: cancellationToken));
        }

        clients.AddRange(await Task.WhenAll(clientTasks));
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    }
}
