using mcp0.Core;

namespace mcp0.Models;

internal sealed record Resource
{
    public required Uri Uri { get; init; }
    public string? MimeType { get; init; }
    public string? Description { get; init; }

    public static Resource Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid resource: {text}");
    }

    public static Resource? TryParse(string text)
    {
        text = text.Trim();
        if (text.Length is 0)
            return null;

        var description = CommandLine.ParseComment(ref text);
        if (text.Length is 0 || Uri.IsWellFormedUriString(text, UriKind.Absolute))
            return null;

        return new() { Uri = new Uri(text, UriKind.Absolute), Description = description };
    }

    public static string? TryFormat(Resource resource)
    {
        if (resource.MimeType is not null)
            return null;

        return CommandLine.FormatComment(resource.Uri.ToString(), resource.Description);
    }
}