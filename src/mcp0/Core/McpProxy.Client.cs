using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Server;

namespace mcp0.Core;

internal sealed partial class McpProxy
{
    public McpClientOptions GetClientOptions() => new()
    {
        Capabilities = new()
        {
            NotificationHandlers = new Dictionary<string, Func<JsonRpcNotification, Task>>(StringComparer.Ordinal)
            {
                { NotificationMethods.PromptListChangedNotification, _ => InitializePrompts(Clients, CancellationToken.None) },
                { NotificationMethods.ResourceListChangedNotification, _ => InitializeResources(Clients, CancellationToken.None) },
                { NotificationMethods.ToolListChangedNotification, _ => InitializeTools(Clients, CancellationToken.None) }
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
                RootsHandler = async (request, cancellationToken) =>
                {
                    ArgumentNullException.ThrowIfNull(request);

                    if (runningServer is null)
                        throw new McpException("Server is not running");

                    return await runningServer.RequestRootsAsync(request, cancellationToken);
                },
                ListChanged = true
            }
        }
    };
}