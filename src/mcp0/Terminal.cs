using System.Text;

namespace mcp0;

internal static class Terminal
{
    public static int Rows => Console.WindowHeight;
    public static int Columns => Console.WindowWidth;
    public static int Row => Console.CursorTop;
    public static int Column => Console.CursorLeft;

    public static void ShowCursor() => Console.CursorVisible = true;
    public static void HideCursor() => Console.CursorVisible = false;
    public static void MoveCursor(int row, int column) => Console.SetCursorPosition(column, row);

    public static ConsoleKeyInfo ReadKey(bool intercept = false) => Console.ReadKey(intercept);

    public static string? ReadLine(
        Func<int, string?>? history = null,
        Func<string, string?>? hint = null,
        ConsoleColor hintColor = ConsoleColor.DarkGray)
    {
        var input = default(ConsoleKeyInfo);
        int row = Row;
        int column = Column;

        var line = string.Empty;
        var cursor = 0;
        var historyIndex = -1;
        var current = (Line: string.Empty, Cursor: cursor);
        var hintLine = (string?)null;

        while (input.Key is not ConsoleKey.Enter)
        {
            if (input.Key is ConsoleKey.Tab)
            {
                line = hintLine ?? line;
                cursor = line.Length;
            }
            else if (input.Key is ConsoleKey.UpArrow)
            {
                if (history?.Invoke(++historyIndex) is { } historicLine)
                {
                    if (historyIndex is 0)
                        current = (line, cursor);

                    line = historicLine;
                    cursor = line.Length;
                }
                else
                    historyIndex--;
            }
            else if (input.Key is ConsoleKey.DownArrow)
            {
                if (historyIndex is not -1)
                {
                    if (--historyIndex is -1)
                    {
                        line = current.Line;
                        cursor = current.Cursor;
                    }
                    else if (history?.Invoke(historyIndex) is { } historicLine)
                    {
                        line = historicLine;
                        cursor = line.Length;
                    }
                }
            }
            else
                EditLine(ref line, ref cursor, input);

            HideCursor();
            ClearLine(row, column);
            Write(line);

            hintLine = hint?.Invoke(line);
            if (hintLine?.Length > line.Length)
                Write(hintLine[line.Length..], hintColor);

            MoveCursor(row, column + cursor);
            ShowCursor();

            input = ReadKey();
        }

        HideCursor();
        ClearLine(row, column);
        WriteLine(line);
        ShowCursor();

        return line;
    }

    private static void EditLine(ref string line, ref int cursor, ConsoleKeyInfo input)
    {
        if (input.Key is ConsoleKey.Backspace)
        {
            if (cursor is not 0 && line.Length is not 0)
                line = line[..--cursor] + line[(cursor + 1)..];
        }
        else if (input.Key is ConsoleKey.Delete)
        {
            if (cursor < line.Length)
                line = line[..cursor] + line[(cursor + 1)..];
        }
        else if (input.Key is ConsoleKey.LeftArrow)
        {
            if (cursor is not 0)
                cursor--;
        }
        else if (input.Key is ConsoleKey.RightArrow)
        {
            if (cursor < line.Length)
                cursor++;
        }
        else if (input.Key is ConsoleKey.Home)
            cursor = 0;
        else if (input.Key is ConsoleKey.End)
            cursor = line.Length;
        else if (!char.IsControl(input.KeyChar))
            line = line[..cursor] + input.KeyChar + line[cursor++..];
    }

    private static void ClearLine(int row, int column)
    {
        MoveCursor(row, column);
        Write(new string(' ', Columns - column));
        MoveCursor(row, column);
    }

    public static void Write(string? text) => Console.Write(text);
    public static void Write(string? text, ConsoleColor color)
    {
        var currentColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = currentColor;
    }

    public static void WriteLine() => Console.WriteLine();
    public static void WriteLine(string? text) => Console.WriteLine(text);
    public static void WriteLine(string? text, ConsoleColor color)
    {
        var currentColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = currentColor;
    }

    public static string Wrap(ReadOnlySpan<char> text, int width, int leftPad = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(leftPad);

        var wraps = text.Length / (width - 1) + 1;
        var buffer = new StringBuilder(text.Length + wraps * (leftPad + 1));
        if (leftPad > 0)
            buffer.Append(' ', leftPad);

        var lineStart = buffer.Length;
        foreach (var lineRange in text.Split('\n'))
        {
            var line = text[lineRange];
            if (line.Length is 0)
            {
                buffer.Append('\n');
                continue;
            }

            if (buffer.Length > leftPad)
                buffer.WrapLine(out lineStart, leftPad);

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
                    buffer.WrapLine(out lineStart, leftPad);

                    while (word.Length > width)
                    {
                        cut = width - 1;
                        buffer.Append(word[0..cut]);
                        word = word[cut..];

                        buffer.Append('\\');
                        buffer.WrapLine(out lineStart, leftPad);
                    }
                }

                buffer.Append(word);

                if (buffer.Length - lineStart >= width)
                {
                    buffer.Length -= word.Length + 1;

                    buffer.WrapLine(out lineStart, leftPad);
                    buffer.Append(word);
                }
            }
        }

        return buffer.ToString();
    }

    private static void WrapLine(this StringBuilder buffer, out int lineStart, int leftPad)
    {
        buffer.Append('\n');
        if (leftPad > 0)
            buffer.Append(' ', leftPad);

        lineStart = buffer.Length;
    }
}