using System.Buffers;

internal static class DotEnv
{
    private static readonly SearchValues<char> validVariableNameChars = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_");

    public static Dictionary<string, string> Parse(ReadOnlySpan<char> envFile)
    {
        var environment = new Dictionary<string, string>();
        var keyValueRanges = (Span<Range>)stackalloc Range[2];

        foreach (var lineRange in envFile.Split('\n'))
        {
            var line = envFile[lineRange];
            if (line.Length is 0 || line[0] is '#' || line.IsWhiteSpace())
                continue;

            if (line.Split(keyValueRanges, '=', StringSplitOptions.TrimEntries) is not 2)
                continue;

            var keySpan = line[keyValueRanges[0]];
            if (keySpan.Length is 0 || keySpan.ContainsAnyExcept(validVariableNameChars))
                continue;

            var key = keySpan.ToString();
            var value = line[keyValueRanges[1]].ToString();

            environment[key] = value;
        }

        return environment;
    }
}
