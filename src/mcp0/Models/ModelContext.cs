using System.Text.Json;
using System.Text.Json.Serialization;

using mcp0.Models.Converters;

namespace mcp0.Models;

[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(StdioServer))]
[JsonSerializable(typeof(SseServer))]
[JsonSourceGenerationOptions(
    Converters = [typeof(PatchConverter),
                  typeof(PromptConverter),
                  typeof(PromptMessageConverter),
                  typeof(ResourceConverter),
                  typeof(ServerConverter),
                  typeof(ServersConverter),
                  typeof(TimeSpanConverter),
                  typeof(ToolConverter),
                  typeof(ToolsConverter)],
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
internal sealed partial class ModelContext : JsonSerializerContext;