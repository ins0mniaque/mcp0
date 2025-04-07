internal static class EnvFile
{
    public static Dictionary<string, string> Parse(ReadOnlySpan<char> envFile)
    {
        var environment = new Dictionary<string, string>();

        foreach (var lineRange in envFile.Split('\n'))
        {
            var line = envFile[lineRange];
            if (line.Length is 0 || line[0] is '#' || line.IsWhiteSpace())
                continue;

            var index = 0;
            var key = string.Empty;
            var value = string.Empty;
            foreach (var keyOrValueRange in line.Split('='))
            {
                if (index is 0) key = line[keyOrValueRange].Trim().ToString();
                else if (index is 1) value = line[keyOrValueRange].Trim().ToString();
                else break;

                index++;
            }

            if (index is not 1 || key.Length is 0 || key[0] is '#')
                continue;

            environment[key] = value;
        }

        return environment;
    }
}
