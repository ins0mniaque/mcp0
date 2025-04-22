namespace mcp0.Core;

internal static class Posix
{
    public static string ExpandPath(string path) => path switch
    {
        ['~', ..] => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..],
        _ => path
    };

    public static string UnexpandPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith(home, StringComparison.Ordinal))
            return '~' + path[home.Length..];

        return path;
    }
}