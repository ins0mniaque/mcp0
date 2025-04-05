using System.Text.Json.Serialization;

[JsonSerializable(typeof(ContextConfig))]
[JsonSerializable(typeof(ServerConfig))]
internal sealed partial class SerializerContext : JsonSerializerContext { }
