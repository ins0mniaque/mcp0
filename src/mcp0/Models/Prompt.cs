namespace mcp0.Models;

internal sealed record Prompt
{
    public required string Template { get; init; }
    public string? Description { get; init; }

    public static Prompt Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid prompt: {text}");
    }

    public static Prompt? TryParse(string text)
    {
        text = text.Trim();
        if (text.Length is 0)
            return null;

        return new() { Template = text };
    }

    public static string? TryFormat(Prompt prompt)
    {
        if (prompt.Description is not null)
            return null;

        return prompt.Template;
    }
}