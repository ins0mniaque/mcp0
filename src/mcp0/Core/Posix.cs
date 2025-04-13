namespace mcp0.Core;

internal static class Posix
{
    public static string ExpandPath(string path) => path switch
    {
        ['~', ..] => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..],
        _ => path
    };
}