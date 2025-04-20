using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Mcp;

internal sealed partial class McpProxy
{
    public void ConfigureClientOptions(McpClientOptions options)
    {
        options.Capabilities = GetClientCapabilities();
    }

    public McpClientOptions GetClientOptions()
    {
        var options = new McpClientOptions();
        ConfigureClientOptions(options);
        return options;
    }

    private ClientCapabilities GetClientCapabilities() => new()
    {
        NotificationHandlers = new Dictionary<string, Func<JsonRpcNotification, CancellationToken, ValueTask>>(StringComparer.Ordinal)
        {
            [NotificationMethods.PromptListChangedNotification] = async (_, cancellationToken) => await InitializePrompts(cancellationToken),
            [NotificationMethods.ResourceListChangedNotification] = async (_, cancellationToken) => await InitializeResources(cancellationToken),
            [NotificationMethods.ToolListChangedNotification] = async (_, cancellationToken) => await InitializeTools(cancellationToken)
        },
        Sampling = new()
        {
            SamplingHandler = async (request, _, cancellationToken) =>
            {
                ArgumentNullException.ThrowIfNull(request);

                if (Server is null)
                    throw new McpException("Server is not running");

                return await Server.RequestSamplingAsync(request, cancellationToken);
            }
        },
        Roots = new()
        {
            ListChanged = true,
            RootsHandler = async (request, cancellationToken) =>
            {
                ArgumentNullException.ThrowIfNull(request);

                if (Server is null)
                    throw new McpException("Server is not running");

                return await Server.RequestRootsAsync(request, cancellationToken);
            }
        }
    };
}