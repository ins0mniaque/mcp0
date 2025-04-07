using System.Text.Json.Serialization;

namespace mcp0.Configuration;

[JsonSerializable(typeof(ContextConfig))]
[JsonSerializable(typeof(ServerConfig))]
internal sealed partial class SerializerContext : JsonSerializerContext { }
