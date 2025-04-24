using mcp0.Core;

namespace mcp0.Models;

internal sealed record Tool
{
    public string Name { get; set; } = string.Empty;
    public required string Command { get; init; }
    public string? Description { get; init; }

    public static Tool Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid tool: {text}");
    }

    public static Tool? TryParse(string text)
    {
        text = text.Trim();
        if (text.Length is 0)
            return null;

        var name = Formattable.ParseAtStart(ref text, ": ", Format.FormattableNameChars);
        if (text.Length is 0)
            return null;

        var description = Formattable.ParseAtEnd(ref text, " #");
        if (text.Length is 0)
            return null;

        return new() { Name = name ?? string.Empty, Command = text, Description = description };
    }

    public static string? TryFormat(Tool tool)
    {
        if (tool.Name.AsSpan().ContainsAnyExcept(Format.FormattableNameChars))
            return null;

        var formatted = Formattable.FormatAtStart(tool.Command, tool.Name, ": ");

        return Formattable.FormatAtEnd(formatted, tool.Description, " # ");
    }
}