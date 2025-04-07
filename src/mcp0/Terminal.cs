using System.Text;

internal static class Terminal
{
    public static int Width => Console.WindowWidth;
    public static int Height => Console.WindowHeight;

    public static void Write(string? text) => Console.Write(text);
    public static void WriteLine(string? text) => Console.WriteLine(text);
    public static void WriteLine() => Console.WriteLine();

    public static void Write(string? text, ConsoleColor foreground)
    {
        var defaultForeground = Console.ForegroundColor;
        Console.ForegroundColor = foreground;
        Console.Write(text);
        Console.ForegroundColor = defaultForeground;
    }

    public static void WriteLine(string? text, ConsoleColor foreground)
    {
        var defaultForeground = Console.ForegroundColor;
        Console.ForegroundColor = foreground;
        Console.WriteLine(text);
        Console.ForegroundColor = defaultForeground;
    }

    public static string Wrap(ReadOnlySpan<char> text, int width, int leftPad = 0)
    {
        var buffer = new StringBuilder(text.Length + (leftPad + 1) * text.Length / width + 1);
        if (leftPad > 0)
            buffer.Append(' ', leftPad);

        var lineStart = buffer.Length;
        foreach (var lineRange in text.Split('\n'))
        {
            if (buffer.Length > leftPad)
                AppendLine(buffer, ref lineStart, leftPad);

            var line = text[lineRange];
            foreach (var range in line.Split(' '))
            {
                if (buffer.Length > lineStart)
                    buffer.Append(' ');

                var word = line[range];

                if (word.Length > width)
                {
                    var cut = width - (buffer.Length - lineStart) - 1;
                    buffer.Append(word[0..cut]);
                    word = word[cut..];
                    buffer.Append('\\');
                    AppendLine(buffer, ref lineStart, leftPad);

                    while (word.Length > width)
                    {
                        cut = width - 1;
                        buffer.Append(word[0..cut]);
                        word = word[cut..];

                        buffer.Append('\\');
                        AppendLine(buffer, ref lineStart, leftPad);
                    }
                }

                buffer.Append(word);

                if (buffer.Length - lineStart >= width)
                {
                    buffer.Length -= word.Length + 1;

                    AppendLine(buffer, ref lineStart, leftPad);
                    buffer.Append(word);
                }
            }
        }

        return buffer.ToString();
    }

    private static void AppendLine(StringBuilder buffer, ref int lineStart, int leftPad)
    {
        buffer.Append('\n');
        if (leftPad > 0)
            buffer.Append(' ', leftPad);

        lineStart = buffer.Length;
    }
}
