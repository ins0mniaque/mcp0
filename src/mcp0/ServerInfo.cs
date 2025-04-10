using System.Reflection;

using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;

namespace mcp0;

internal static class ServerInfo
{
    private static AssemblyName DefaultAssemblyName { get; } = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName();

    public static Implementation Default { get; } = new()
    {
        Name = DefaultAssemblyName.Name ?? nameof(mcp0),
        Version = DefaultAssemblyName.Version?.ToString() ?? "1.0.0",
    };

    public static Implementation Create(IEnumerable<IClientTransport> transports) => new()
    {
        Name = string.Join('/', transports.Select(static transport => transport.Name).DefaultIfEmpty(Default.Name)),
        Version = Default.Version
    };
}