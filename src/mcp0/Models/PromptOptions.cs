using System.Text.Json.Serialization;

using mcp0.Models.Converters;

namespace mcp0.Models;

internal sealed record PromptOptions
{
    [JsonConverter(typeof(StringArrayOrStringConverter))]
    public string[]? Model { get; init; }
    public PromptContext? Context { get; init; }
    public string? SystemPrompt { get; init; }
    public int? MaxTokens { get; init; }
    public string[]? StopSequences { get; init; }
    public float? Temperature { get; init; }
}