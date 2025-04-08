using System.Buffers;

namespace mcp0.Core;

internal static class DotEnv
{
    private static readonly SearchValues<char> ValidKeyChars = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_");

    public static Dictionary<string, string> Parse(ReadOnlySpan<char> envFile)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        var keyValueRanges = (Span<Range>)stackalloc Range[2];

        foreach (var lineRange in envFile.Split('\n'))
        {
            var line = envFile[lineRange];
            if (line.Length is 0 || line[0] is '#' || line.IsWhiteSpace())
                continue;

            if (line.Split(keyValueRanges, '=', StringSplitOptions.TrimEntries) is not 2)
                continue;

            var key = line[keyValueRanges[0]];
            if (key.Length is 0 || key.ContainsAnyExcept(ValidKeyChars))
                continue;

            environment[key.ToString()] = line[keyValueRanges[1]].ToString();
        }

        return environment;
    }
}