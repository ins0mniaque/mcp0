using System.Buffers;

namespace mcp0.Core;

internal static class Formattable
{
    public static string? ParseAtStart(ref string text, ReadOnlySpan<char> separator, SearchValues<char>? validChars = null)
    {
        var span = text.AsSpan();
        if (span.Length is 0)
            return null;

        var index = span.IndexOf(separator, StringComparison.Ordinal);
        if (index is not -1)
        {
            if (validChars is not null && span[..index].ContainsAnyExcept(validChars))
                return null;

            text = span[(index + separator.Length)..].Trim().ToString();
            return span[..index].Trim().ToString();
        }

        var trimmed = separator.TrimEnd();
        if (span.EndsWith(trimmed, StringComparison.Ordinal))
        {
            if (validChars is not null && span[..trimmed.Length].ContainsAnyExcept(validChars))
                return null;

            text = string.Empty;
            return span[..trimmed.Length].Trim().ToString();
        }

        return null;
    }

    public static string? ParseAtEnd(ref string text, ReadOnlySpan<char> separator, SearchValues<char>? validChars = null)
    {
        var span = text.AsSpan();
        if (span.Length is 0)
            return null;

        var index = span.LastIndexOf(separator, StringComparison.Ordinal);
        if (index is not -1)
        {
            if (validChars is not null && span[(index + separator.Length)..].ContainsAnyExcept(validChars))
                return null;

            text = span[..index].Trim().ToString();
            return span[(index + separator.Length)..].Trim().ToString();
        }

        var trimmed = separator.TrimStart();
        if (span.StartsWith(trimmed, StringComparison.Ordinal))
        {
            if (validChars is not null && span[trimmed.Length..].ContainsAnyExcept(validChars))
                return null;

            text = string.Empty;
            return span[trimmed.Length..].Trim().ToString();
        }

        return null;
    }

    public static string? FormatAtStart(string? text, string? value, ReadOnlySpan<char> separator)
    {
        if (value is null || value.Length is 0)
            return text;

        if (text is null || text.Length is 0)
            return $"{value}{separator.TrimEnd()}";

        return $"{value}{separator}{text}";
    }

    public static string? FormatAtEnd(string? text, string? value, ReadOnlySpan<char> separator)
    {
        if (value is null || value.Length is 0)
            return text;

        if (text is null || text.Length is 0)
            return $"{separator.TrimStart()}{value}";

        return $"{text}{separator}{value}";
    }
}