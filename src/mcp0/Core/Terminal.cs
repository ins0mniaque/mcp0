using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace mcp0.Core;

internal static class Terminal
{
    public const ConsoleColor DefaultColor = (ConsoleColor)(-1);

    public static int Rows => Console.WindowHeight;
    public static int Columns => Console.WindowWidth;

    internal static class Cursor
    {
        public static int Row => Console.CursorTop;
        public static int Column => Console.CursorLeft;

        public static void Show() => Console.CursorVisible = true;
        public static void Hide() => Console.CursorVisible = false;
        public static void Move(int row, int column) => Console.SetCursorPosition(column, row);
    }

    public static Stream OpenStdIn() => Console.OpenStandardInput();
    public static Stream OpenStdErr() => Console.OpenStandardError();
    public static Stream OpenStdOut() => Console.OpenStandardOutput();

    public static ConsoleKeyInfo ReadKey(bool intercept = false) => Console.ReadKey(intercept);

    public static string ReadLine(
        Func<int, string?>? history = null,
        Func<string, string?>? hint = null,
        ConsoleColor hintColor = ConsoleColor.DarkGray)
    {
        var input = default(ConsoleKeyInfo);
        int row = Cursor.Row;
        int column = Cursor.Column;
        var viewport = Columns - column;

        var line = string.Empty;
        var clearLine = new string(' ', viewport);
        var cursor = 0;
        var scroll = 0;
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

            if (cursor >= scroll + viewport - 1)
                scroll = cursor - (viewport - 1);
            else if (cursor < scroll)
                scroll = cursor;

            Cursor.Hide();
            Cursor.Move(row, column);
            Write(clearLine);
            Cursor.Move(row, column);
            Write(LineView(line, 0));

            hintLine = hint?.Invoke(line);
            if (hintLine?.Length > line.Length && line.Length - scroll < viewport - 1)
                Write(LineView(hintLine, line.Length), hintColor);

            Cursor.Move(row, column + cursor - scroll);
            Cursor.Show();

            input = ReadKey();
        }

        Cursor.Hide();
        Cursor.Move(row, column);
        Write(clearLine);
        Cursor.Move(row, column);
        WriteLine(line);
        Cursor.Show();

        return line;

        ReadOnlySpan<char> LineView(ReadOnlySpan<char> span, int start)
        {
            return span[(scroll + start)..(Math.Min(span.Length, scroll + viewport))];
        }
    }

    private static void EditLine(ref string line, ref int cursor, ConsoleKeyInfo input)
    {
        var span = line.AsSpan();

        if (input.Key is ConsoleKey.Backspace)
        {
            if (cursor is 0)
                return;

            var until = IsCtrlOrAlt(input) ? span.IndexOfPreviousWord(cursor) : cursor - 1;
            line = string.Concat(span[..until], span[cursor..]);
            cursor = until;
        }
        else if (input.Key is ConsoleKey.Delete)
        {
            if (cursor >= line.Length)
                return;

            var until = IsCtrlOrAlt(input) ? span.IndexOfNextWord(cursor) : cursor + 1;
            line = string.Concat(span[..cursor], span[until..]);
        }
        else if (input.Key is ConsoleKey.LeftArrow)
        {
            if (cursor is not 0)
                cursor = IsCtrlOrAlt(input) ? span.IndexOfPreviousWord(cursor) : cursor - 1;
        }
        else if (input.Key is ConsoleKey.RightArrow)
        {
            if (cursor < line.Length)
                cursor = IsCtrlOrAlt(input) ? span.IndexOfNextWord(cursor) : cursor + 1;
        }
        else if (input.Key is ConsoleKey.Home)
            cursor = 0;
        else if (input.Key is ConsoleKey.End)
            cursor = line.Length;
        else if (!char.IsControl(input.KeyChar))
            line = string.Concat(span[..cursor], [input.KeyChar], span[cursor++..]);
    }

    private static bool IsCtrlOrAlt(ConsoleKeyInfo input)
    {
        return (input.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) is not ConsoleModifiers.None;
    }

    private const string WordChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";
    private static readonly SearchValues<char> wordChars = SearchValues.Create(WordChars);
    private static readonly SearchValues<char> wordCharsOrSpace = SearchValues.Create(WordChars + ' ');

    private static int IndexOfPreviousWord(this ReadOnlySpan<char> line, int cursor)
    {
        cursor = line[..cursor].LastIndexOfAnyExcept(' ');
        if (cursor is -1)
            return 0;

        if (wordChars.Contains(line[cursor]))
            return line[..cursor].LastIndexOfAnyExcept(wordChars) + 1;

        return line[..cursor].LastIndexOfAny(wordCharsOrSpace) + 1;
    }

    private static int IndexOfNextWord(this ReadOnlySpan<char> line, int cursor)
    {
        var offset = 0;
        if (wordChars.Contains(line[cursor]))
            offset = line[cursor..].IndexOfAnyExcept(wordChars);
        else if (line[cursor] is not ' ')
            offset = line[cursor..].IndexOfAny(wordCharsOrSpace);

        if (offset is -1)
            return line.Length;

        cursor += offset;
        offset = line[cursor..].IndexOfAnyExcept(' ');

        return offset is -1 ? line.Length : cursor + offset;
    }

    public static void Write(InterpolatedStringHandler text) { }
    public static void Write(ReadOnlySpan<char> text) => Console.Out.Write(text);

    public static void Write(ReadOnlySpan<char> text, ConsoleColor color)
    {
        var currentColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Out.Write(text);
        Console.ForegroundColor = currentColor;
    }

    public static void WriteLine() => Console.Out.WriteLine();
    public static void WriteLine(InterpolatedStringHandler text) => Console.Out.WriteLine();
    public static void WriteLine(ReadOnlySpan<char> text) => Console.Out.WriteLine(text);

    public static void WriteLine(ReadOnlySpan<char> text, ConsoleColor color)
    {
        var currentColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Out.WriteLine(text);
        Console.ForegroundColor = currentColor;
    }

    public static string WordWrap(ReadOnlySpan<char> text, int width, int leftPad = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(leftPad);

        var bufferSize = EstimateWrapBufferSize(text, width, leftPad);
        var buffer = new StringBuilder(bufferSize);

        return Wrap(buffer, text, width, leftPad);
    }

    public static string WordWrap(StringBuilder buffer, ReadOnlySpan<char> text, int width, int leftPad = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(leftPad);

        return Wrap(buffer, text, width, leftPad);
    }

    private static string Wrap(StringBuilder buffer, ReadOnlySpan<char> text, int width, int leftPad)
    {
        buffer.Clear();

        var bufferSize = EstimateWrapBufferSize(text, width, leftPad);
        if (buffer.Capacity < bufferSize)
            buffer.Capacity = bufferSize;

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

    private static int EstimateWrapBufferSize(ReadOnlySpan<char> text, int width, int leftPad)
    {
        var wraps = text.Length / (width - 1) + 1;
        return text.Length + wraps * (leftPad + 1);
    }

    [InterpolatedStringHandler]
    [SuppressMessage("Performance", "CA1822:Mark member as static", Justification = "InterpolatedStringHandler")]
    internal struct InterpolatedStringHandler
    {
        [SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "InterpolatedStringHandler")]
        public InterpolatedStringHandler(int literalLength, int formattedCount) { }

        public void AppendLiteral(string text) => Write(text);
        public void AppendFormatted<T>(T typed)
        {
            if (typed is ConsoleColor color)
                Console.ForegroundColor = color;
            else
                Write(typed?.ToString());
        }
    }
}