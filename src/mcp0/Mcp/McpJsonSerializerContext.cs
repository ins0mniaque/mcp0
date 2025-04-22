using System.Text.Json.Serialization;

using ModelContextProtocol.Protocol.Types;

namespace mcp0.Mcp;

[JsonSerializable(typeof(CreateMessageRequestParams))]
[JsonSerializable(typeof(CreateMessageResult))]
internal sealed partial class McpJsonSerializerContext : JsonSerializerContext;