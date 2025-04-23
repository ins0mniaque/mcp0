using ModelContextProtocol.Protocol.Types;

namespace mcp0.Mcp;

internal sealed class McpProxyOptions
{
    public Implementation? ServerInfo { get; set; }
    public SamplingCapability? Sampling { get; set; }
    public LoggingLevel? LoggingLevel { get; set; }
    public Action<LoggingLevel>? SetLoggingLevelCallback { get; set; }
    public McpProxyMaps? Maps { get; set; }
}