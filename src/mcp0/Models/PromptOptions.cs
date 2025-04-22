namespace mcp0.Models;

internal sealed record PromptOptions
{
    public string? Model { get; init; }
    public string[]? ModelHints { get; init; }
    public PromptContext? Context { get; init; }
    public string? SystemPrompt { get; init; }
    public int? MaxTokens { get; init; }
    public string[]? StopSequences { get; init; }
    public float? Temperature { get; init; }
}