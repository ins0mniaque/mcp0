using ModelContextProtocol.Protocol.Types;

namespace mcp0.Mcp;

internal sealed class McpProxyMaps
{
    public Func<Prompt, Prompt?>? Prompt { get; set; }
    public Func<Resource, Resource?>? Resource { get; set; }
    public Func<ResourceTemplate, ResourceTemplate?>? ResourceTemplate { get; set; }
    public Func<Tool, Tool?>? Tool { get; set; }
}