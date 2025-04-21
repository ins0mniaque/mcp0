using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models;

internal sealed class ServersConverter : KeyedListConverter<Server>
{
    protected override JsonTypeInfo<Server> JsonTypeInfo => ModelContext.Default.Server;

    protected override Func<Server, string?> GetKey { get; } = static server => server.Name;
    protected override Func<Server, string?, Server> SetKey { get; } = static (server, name) =>
    {
        server.Name = name;
        return server;
    };
}