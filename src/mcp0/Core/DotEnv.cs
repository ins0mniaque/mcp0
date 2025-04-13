using System.Buffers;

namespace mcp0.Core;

internal static class DotEnv
{
    private static readonly SearchValues<char> validKeyChars = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_");

    public static Dictionary<string, string> Parse(ReadOnlySpan<char> dotEnv)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        var keyValueRanges = (Span<Range>)stackalloc Range[2];

        foreach (var lineRange in dotEnv.Split('\n'))
        {
            var line = dotEnv[lineRange];
            if (line.Length is 0 || line[0] is '#' || line.IsWhiteSpace())
                continue;

            if (!Split(line, keyValueRanges))
                continue;

            var key = line[keyValueRanges[0]];
            var value = line[keyValueRanges[1]];
            if (!IsValidKey(key))
                continue;

            environment[key.ToString()] = value.ToString();
        }

        return environment;
    }

    public static bool Split(ReadOnlySpan<char> line, Span<Range> keyValueRanges)
    {
        return line.Split(keyValueRanges, '=', StringSplitOptions.TrimEntries) is 2;
    }

    public static bool IsValidKey(ReadOnlySpan<char> key)
    {
        return key.Length is not 0 && !key.ContainsAnyExcept(validKeyChars);
    }
}