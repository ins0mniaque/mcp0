using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models;

[JsonSourceGenerationOptions(
    Converters = [typeof(PatchConverter), typeof(ServerConverter), typeof(ServersConverter)],
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(Patch))]
[JsonSerializable(typeof(Server))]
[JsonSerializable(typeof(StdioServer))]
[JsonSerializable(typeof(SseServer))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ModelContext : JsonSerializerContext;