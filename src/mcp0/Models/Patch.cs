using System.Text.Json.Serialization;

using mcp0.Core;

namespace mcp0.Models;

[JsonConverter(typeof(PatchConverter))]
internal sealed record Patch
{
    public static Patch Remove { get; } = new();

    public string? Name { get; init; }
    public string? Description { get; init; }

    public static Patch? FromString(string text)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        if (text[0] is '#')
            return new() { Description = text[1..].TrimStart() };

        var description = CommandLine.ParseComment(ref text);

        return new() { Name = text, Description = description };
    }
}