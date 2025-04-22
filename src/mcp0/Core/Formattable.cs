namespace mcp0.Core;

internal static class Formattable
{
    public static string? Parse(ref string text, ReadOnlySpan<char> separator)
    {
        if (text.Length is 0)
            return null;

        var span = text.AsSpan();
        if (span.StartsWith(separator))
        {
            text = string.Empty;
            return span[separator.Length..].Trim().ToString();
        }

        var index = span.LastIndexOf([' ', .. separator], StringComparison.Ordinal);
        if (index is -1)
            return null;

        text = span[..index].Trim().ToString();
        return span[(index + separator.Length + 1)..].Trim().ToString();
    }

    public static string? Format(string? text, string? value, ReadOnlySpan<char> separator)
    {
        if (value is null || value.Length is 0)
            return text;

        if (text is null || text.Length is 0)
            return $"{separator} {value}";

        return $"{text} {separator} {value}";
    }
}