namespace mcp0.Core;

internal static class Formattable
{
    public static string? ParseAtStart(ref string text, ReadOnlySpan<char> separator)
    {
        var span = text.AsSpan();
        if (span.Length is 0)
            return null;

        var index = span.IndexOf(separator, StringComparison.Ordinal);
        if (index is not -1)
        {
            text = span[(index + separator.Length)..].Trim().ToString();
            return span[..index].Trim().ToString();
        }

        var trimmed = separator.TrimEnd();
        if (span.EndsWith(trimmed, StringComparison.Ordinal))
        {
            text = string.Empty;
            return span[..trimmed.Length].Trim().ToString();
        }

        return null;
    }

    public static string? ParseAtEnd(ref string text, ReadOnlySpan<char> separator)
    {
        var span = text.AsSpan();
        if (span.Length is 0)
            return null;

        var index = span.LastIndexOf(separator, StringComparison.Ordinal);
        if (index is not -1)
        {
            text = span[..index].Trim().ToString();
            return span[(index + separator.Length)..].Trim().ToString();
        }

        var trimmed = separator.TrimStart();
        if (span.StartsWith(trimmed, StringComparison.Ordinal))
        {
            text = string.Empty;
            return span[trimmed.Length..].Trim().ToString();
        }

        return null;
    }

    public static string? Format(string? text, string? value, ReadOnlySpan<char> separator)
    {
        if (value is null || value.Length is 0)
            return text;

        if (text is null || text.Length is 0)
            return $"{separator.TrimStart()}{value}";

        return $"{text}{separator}{value}";
    }
}