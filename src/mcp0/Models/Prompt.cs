namespace mcp0.Models;

internal sealed record Prompt
{
    public required PromptMessage[] Messages { get; init; }
    public PromptOptions? Options { get; init; }
    public string? Description { get; init; }

    public static Prompt Parse(string text)
    {
        return new() { Messages = [PromptMessage.Parse(text)] };
    }

    public static Prompt? TryParse(string text)
    {
        if (PromptMessage.TryParse(text) is { } message)
            return new() { Messages = [message] };

        return null;
    }

    public static string? TryFormat(Prompt prompt)
    {
        if (prompt.Description is not null || prompt.Messages.Length is not 1)
            return null;

        return PromptMessage.TryFormat(prompt.Messages[0]);
    }
}