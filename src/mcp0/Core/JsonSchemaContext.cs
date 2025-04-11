using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Core;

[JsonSerializable(typeof(JsonElement))]
internal sealed partial class JsonSchemaContext : JsonSerializerContext { }