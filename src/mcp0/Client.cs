using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;

internal sealed class Client
{
    private readonly ILoggerFactory loggerFactory;

    public Client(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    public IReadOnlyList<IMcpClient> Clients { get; } = new List<IMcpClient>();

    public async Task Initialize(IEnumerable<McpServerConfig> servers, CancellationToken cancellationToken)
    {
        var clientTasks = new List<Task<IMcpClient>>();
        foreach (var server in servers)
        {
            clientTasks.Add(McpClientFactory.CreateAsync(
                server,
                loggerFactory: loggerFactory,
                cancellationToken: cancellationToken));
        }

        var clients = await Task.WhenAll(clientTasks);
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    }
}
