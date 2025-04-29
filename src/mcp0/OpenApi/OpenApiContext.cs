using System.Text.Json.Serialization;

using ModelContextProtocol.Protocol.Types;

namespace mcp0.OpenApi;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OpenApiDocument))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(List<Content>))]
internal sealed partial class OpenApiContext : JsonSerializerContext;