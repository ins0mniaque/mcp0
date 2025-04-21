using mcp0.Core;

namespace mcp0.Models;

internal sealed record Patch
{
    public static Patch Remove { get; } = new();

    public string? Name { get; init; }
    public string? Description { get; init; }

    public static Patch Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid patch: {text}");
    }

    public static Patch? TryParse(string text)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        return new()
        {
            Description = CommandLine.ParseComment(ref text),
            Name = text.Length is 0 ? null : text
        };
    }

    public static string? Format(Patch patch)
    {
        if (patch == Remove)
            return null;

        if (patch.Description is null || patch.Description.Length is 0)
            return patch.Name;

        if (patch.Name is null || patch.Name.Length is 0)
            return $"# {patch.Description}";

        return $"{patch.Name} # {patch.Description}";
    }
}