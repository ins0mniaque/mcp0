internal static class Terminal
{
    public static void Write(string? value) => Console.Write(value);
    public static void WriteLine(string? value) => Console.WriteLine(value);
    public static void WriteLine() => Console.WriteLine();

    public static void Write(string? value, ConsoleColor foreground)
    {
        var defaultForeground = Console.ForegroundColor;
        Console.ForegroundColor = foreground;
        Console.Write(value);
        Console.ForegroundColor = defaultForeground;
    }

    public static void WriteLine(string? value, ConsoleColor foreground)
    {
        var defaultForeground = Console.ForegroundColor;
        Console.ForegroundColor = foreground;
        Console.WriteLine(value);
        Console.ForegroundColor = defaultForeground;
    }
}
