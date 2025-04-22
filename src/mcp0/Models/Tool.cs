using mcp0.Core;

namespace mcp0.Models;

internal sealed record Tool
{
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

        var description = Formattable.Parse(ref text, "#");
        if (text.Length is 0)
            return null;

        return new() { Command = text, Description = description };
    }

    public static string? TryFormat(Tool tool)
    {
        return Formattable.Format(tool.Command, tool.Description, "#");
    }
}