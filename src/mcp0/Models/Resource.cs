using System.Text.Json.Serialization;

using mcp0.Core;
using mcp0.Models.Converters;

namespace mcp0.Models;

internal sealed record Resource
{
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

        var description = Formattable.ParseAtEnd(ref text, " #");
        if (text.Length is 0 || ResourceUriConverter.TryConvert(text) is not { } uri)
            return null;

        return new() { Uri = uri, Description = description };
    }

    public static string? TryFormat(Resource resource)
    {
        if (resource.MimeType is not null)
            return null;

        return Formattable.FormatAtEnd(ResourceUriConverter.Convert(resource.Uri), resource.Description, " # ");
    }
}