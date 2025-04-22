using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models.Converters;

[JsonSourceGenerationOptions(
    Converters = [typeof(TimeSpanConverter)],
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(Patch))]
[JsonSerializable(typeof(Prompt))]
[JsonSerializable(typeof(Resource))]
[JsonSerializable(typeof(Server))]
[JsonSerializable(typeof(StdioServer))]
[JsonSerializable(typeof(SseServer))]
[JsonSerializable(typeof(Tool))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ConverterContext : JsonSerializerContext;