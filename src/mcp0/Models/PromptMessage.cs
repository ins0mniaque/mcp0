using mcp0.Core;

namespace mcp0.Models;

internal sealed record PromptMessage
{
    public required string Template { get; init; }
    public string? ReturnArgument { get; init; }
    public PromptOptions? Options { get; init; }

    public static PromptMessage Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid prompt: {text}");
    }

    public static PromptMessage? TryParse(string text)
    {
        // TODO: Support formattable options
        text = text.Trim();
        if (text.Length is 0)
            return null;

        return new()
        {
            ReturnArgument = Formattable.Parse(ref text, "=>")?.TrimStart('{').TrimEnd('}'),
            Template = text
        };
    }

    public static string? TryFormat(PromptMessage message)
    {
        // TODO: Support formattable options
        if (message.Options is not null)
            return null;

        if (message.ReturnArgument is null)
            return message.Template;

        return Formattable.Format(message.Template, $"{{{{{message.ReturnArgument}}}}}", "=>");
    }
}