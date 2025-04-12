using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Mcp;

internal sealed partial class McpProxy
{
    public McpClientOptions GetClientOptions() => new()
    {
        Capabilities = GetClientCapabilities()
    };

    private ClientCapabilities GetClientCapabilities() => new()
    {
        NotificationHandlers = new Dictionary<string, Func<JsonRpcNotification, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            { NotificationMethods.PromptListChangedNotification, (_, cancellationToken) => InitializePrompts(Clients, cancellationToken) },
            { NotificationMethods.ResourceListChangedNotification, (_, cancellationToken) => InitializeResources(Clients, cancellationToken) },
            { NotificationMethods.ToolListChangedNotification, (_, cancellationToken) => InitializeTools(Clients, cancellationToken) }
        },
        Sampling = new()
        {
            SamplingHandler = async (request, _, cancellationToken) =>
            {
                ArgumentNullException.ThrowIfNull(request);

                if (runningServer is null)
                    throw new McpException("Server is not running");

                return await runningServer.RequestSamplingAsync(request, cancellationToken);
            }
        },
        Roots = new()
        {
            ListChanged = true,
            RootsHandler = async (request, cancellationToken) =>
            {
                ArgumentNullException.ThrowIfNull(request);

                if (runningServer is null)
                    throw new McpException("Server is not running");

                return await runningServer.RequestRootsAsync(request, cancellationToken);
            }
        }
    };
}