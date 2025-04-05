internal static class EnvironmentFileFormat
{
    public static Dictionary<string, string> Read(string path)
    {
        var environment = new Dictionary<string, string>();

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                continue;

            var parts = line.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (string.IsNullOrWhiteSpace(key) || key[0] == '#')
                continue;

            environment[key] = value;
        }

        return environment;
    }
}
