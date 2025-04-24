using mcp0.Core;

namespace mcp0.Models;

internal sealed record Prompt
{
    public string Name { get; set; } = string.Empty;
    public required PromptMessage[] Messages { get; init; }
    public PromptOptions? Options { get; init; }
    public string? Description { get; init; }

    public static Prompt Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid prompt: {text}");
    }

    public static Prompt? TryParse(string text)
    {
        var name = Formattable.ParseAtStart(ref text, ": ", Format.FormattableNameChars);
        if (text.Length is 0)
            return null;

        if (PromptMessage.TryParse(text) is { } message)
            return new() { Name = name ?? string.Empty, Messages = [message] };

        return null;
    }

    public static string? TryFormat(Prompt prompt)
    {
        if (prompt.Description is not null || prompt.Messages.Length is not 1)
            return null;

        if (prompt.Name.AsSpan().ContainsAnyExcept(Format.FormattableNameChars))
            return null;

        if (PromptMessage.TryFormat(prompt.Messages[0]) is not { } formatted)
            return null;

        return Formattable.FormatAtStart(formatted, prompt.Name, ": ");
    }

    public static void Validate(Prompt prompt)
    {
        if (!string.IsNullOrWhiteSpace(prompt.Name))
            return;

        var description = prompt.Description;
        if (description is null && prompt.Messages.Length is not 0)
            description = prompt.Messages[0].Template;

        throw new FormatException($"Missing name for prompt: {description}");
    }
}