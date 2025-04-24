using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models.Converters;

internal sealed class ServersConverter : KeyedListConverter<Server>
{
    protected override JsonTypeInfo<Server> JsonTypeInfo => ModelContext.Default.Server;

    protected override Func<Server, string?> GetKey { get; } = static server => server.Name;
    protected override Action<Server, string?> SetKey { get; } = static (server, name) => server.Name = name;
}