using System.Text.Json.Serialization;

namespace mcp0.Model;

[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(Server))]
internal sealed partial class Model : JsonSerializerContext { }