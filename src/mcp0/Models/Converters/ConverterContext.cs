using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(StdioServer))]
[JsonSerializable(typeof(SseServer))]
[JsonSourceGenerationOptions(
    Converters = [typeof(PromptMessagesConverter), typeof(TimeSpanConverter)],
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
internal sealed partial class ConverterContext : JsonSerializerContext;