using System.Text.Json.Serialization;

using mcp0.Core;
using mcp0.Models.Converters;

namespace mcp0.Models;

internal sealed record Resource
{
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(ResourceUriConverter))]
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

        var name = Formattable.ParseAtStart(ref text, ": ", Format.FormattableNameChars);
        if (text.Length is 0)
            return null;

        var description = Formattable.ParseAtEnd(ref text, " #");
        if (text.Length is 0 || ResourceUriConverter.TryConvert(text) is not { } uri)
            return null;

        return new() { Name = name ?? string.Empty, Uri = uri, Description = description };
    }

    public static string? TryFormat(Resource resource)
    {
        if (resource.MimeType is not null)
            return null;

        if (resource.Name.AsSpan().ContainsAnyExcept(Format.FormattableNameChars))
            return null;

        var formatted = Formattable.FormatAtStart(ResourceUriConverter.Convert(resource.Uri), resource.Name, ": ");

        return Formattable.FormatAtEnd(formatted, resource.Description, " # ");
    }
}