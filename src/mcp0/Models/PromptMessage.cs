using mcp0.Core;

namespace mcp0.Models;

internal sealed record PromptMessage
{
    public required string Template { get; init; }
    public string? ReturnArgument { get; init; }
    public PromptOptions? Options { get; init; }

    public static PromptMessage Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid prompt message: {text}");
    }

    public static PromptMessage? TryParse(string text)
    {
        text = text.Trim();
        if (text.Length is 0)
            return null;

        return new()
        {
            ReturnArgument = Formattable.ParseAtEnd(ref text, "=>")?.TrimStart('{').TrimEnd('}'),
            Template = text
        };
    }

    public static string? TryFormat(PromptMessage message)
    {
        var options = message.Options;
        var formattable = options is null;
        if (!formattable)
            return null;

        var formatted = message.Template;
        if (message.ReturnArgument is not null)
            formatted = Formattable.Format(formatted, $"{{{{{message.ReturnArgument}}}}}", " => ");

        return formatted;
    }
}