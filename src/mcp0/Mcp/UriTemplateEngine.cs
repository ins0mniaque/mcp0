using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace mcp0.Mcp;

internal static class UriTemplateEngine
{
    internal static string Render(string template, IReadOnlyDictionary<string, object?> values)
    {
        return Render(template, (_, token) => values.GetValueOrDefault(token.ToString()), regex: false);
    }

    internal static Regex CreateParser(string template, RegexOptions options)
    {
        var pattern = Render(template, GetTokenCapturePattern, regex: true);

        return new Regex(AddStartAndEndAnchors(pattern), options, TimeSpan.FromSeconds(1));
    }

    private static string GetTokenCapturePattern(Operator op, StringBuilder token) => op switch
    {
        Operator.QuestionMark or Operator.Ampersand => $"(?<{token}>[^&]*)",
        _ => $"(?<{token}>[^/?]*)"
    };

    private static string AddStartAndEndAnchors(string pattern)
    {
        return string.Create(pattern.Length + 2, pattern, static (chars, pattern) =>
        {
            chars[0] = '^';
            pattern.CopyTo(chars[1..]);
            chars[^1] = '$';
        });
    }

    internal static Dictionary<string, object?>? Parse(Regex parser, string uri)
    {
        var match = parser.Match(uri);
        if (!match.Success)
            return null;

        return match.Groups.Cast<Group>()
                           .Where(static group => group.Name is not "0")
                           .ToDictionary(static group => group.Name,
                                         static group => (object?)group.Value,
                                         StringComparer.Ordinal);
    }

    private enum Operator { None, NoOp, Plus, Hash, Dot, Slash, Semicolon, QuestionMark, Ampersand }

    private static string Render(string template, Func<Operator, StringBuilder, object?> replaceToken, bool regex)
    {
        var uri = new StringBuilder(template.Length * 2);
        var maxTokenLengthBuffer = new StringBuilder(3);
        var insideMaxTokenLength = false;

        var token = new StringBuilder();
        var insideToken = false;
        var firstToken = true;
        var op = Operator.None;
        var composite = false;

        for (var index = 0; index < template.Length; index++)
        {
            var character = template[index];

            if (character is '{')
            {
                insideToken = true;
                token.Clear();
                firstToken = true;
                continue;
            }

            if (character is '}')
            {
                if (!insideToken)
                    throw new FormatException($"Invalid character '{character}' in template at position {index}");

                var maxTokenLength = maxTokenLengthBuffer.GetMaxTokenLength(index);
                var rendered = uri.RenderToken(op, token, composite, maxTokenLength, firstToken, replaceToken, index, regex);
                if (rendered && firstToken)
                    firstToken = false;

                insideToken = false;
                token.Clear();
                op = Operator.None;
                composite = false;
                insideMaxTokenLength = false;
                maxTokenLengthBuffer.Clear();

                continue;
            }

            if (character is ',' && insideToken)
            {
                var maxTokenLength = maxTokenLengthBuffer.GetMaxTokenLength(index);
                var rendered = uri.RenderToken(op, token, composite, maxTokenLength, firstToken, replaceToken, index, regex);
                if (rendered && firstToken)
                    firstToken = false;

                token.Clear();
                composite = false;
                insideMaxTokenLength = false;
                maxTokenLengthBuffer.Clear();
                continue;
            }

            if (insideToken)
            {
                if (op is Operator.None)
                {
                    op = token.GetOperator(character, index);
                }
                else if (insideMaxTokenLength)
                {
                    if (char.IsDigit(character))
                        maxTokenLengthBuffer.Append(character);
                    else
                        throw new FormatException($"Invalid character '{character}' in maximum token length at position {index}");
                }
                else
                {
                    if (character is ':')
                    {
                        insideMaxTokenLength = true;
                        maxTokenLengthBuffer.Clear();
                    }
                    else if (character is '*')
                        composite = true;
                    else
                        token.AppendLiteral(character, index);
                }
            }
            else if (regex)
            {
                if (character is '\t' or '\n' or '\f' or '\r' or ' ' or '#' or '$' or '(' or ')' or '*' or '+' or '.' or '?' or '[' or '\\' or '^' or '{' or '|')
                    uri.Append('\\');

                uri.Append(character switch
                {
                    '\t' => 't',
                    '\n' => 'n',
                    '\f' => 'f',
                    '\r' => 'r',
                    _ => character
                });
            }
            else
                uri.Append(character);
        }

        if (!insideToken)
            return uri.ToString();

        throw new FormatException("Unterminated token");
    }

    private static Operator GetOperator(this StringBuilder token, char character, int position)
    {
        if (character is '+') return Operator.Plus;
        if (character is '#') return Operator.Hash;
        if (character is '.') return Operator.Dot;
        if (character is '/') return Operator.Slash;
        if (character is ';') return Operator.Semicolon;
        if (character is '?') return Operator.QuestionMark;
        if (character is '&') return Operator.Ampersand;

        token.AppendLiteral(character, position);
        return Operator.NoOp;
    }

    private static int GetMaxTokenLength(this StringBuilder buffer, int position)
    {
        if (buffer.Length is 0)
            return -1;

        var maxTokenLength = buffer.ToString();
        if (int.TryParse(maxTokenLength, CultureInfo.InvariantCulture, out var length))
            return length;

        throw new FormatException($"Invalid maximum token length '{maxTokenLength}' at position {position}");
    }

    private static bool RenderToken(
        this StringBuilder uri, Operator op, StringBuilder token, bool composite, int maxTokenLength, bool firstToken,
        Func<Operator, StringBuilder, object?> replaceToken, int position, bool regex)
    {
        if (token.Length is 0)
            throw new FormatException($"Empty token at position {position}");

        var value = replaceToken(op, token);
        if (IsEmpty(value))
            return false;

        if (firstToken)
            uri.AppendPrefix(op, regex);
        else
            uri.AppendSeparator(op);

        if (value is string or bool or int or long or float or double or decimal)
            uri.AppendValue(op, token, value, maxTokenLength, regex);
        else if (value is IList list)
            uri.AppendList(op, token, list, maxTokenLength, composite, regex);
        else if (value is IDictionary dictionary)
            uri.AppendDictionary(op, token, dictionary, maxTokenLength, composite, regex);
        else
            throw new InvalidOperationException($"Invalid value type {value.GetType().Name} passed as replacement for token '{token}' at position {position}");

        return true;
    }

    private static void AppendLiteral(this StringBuilder token, char character, int position)
    {
        if (character is '+' or '#' or '/' or ';' or '?' or '&' or ' ' or '!' or '=' or '$' or '|' or '*' or ':' or '~' or '-')
            throw new FormatException($"Invalid character '{character}' in token at position {position}");

        token.Append(character);
    }

    private static void AppendPrefix(this StringBuilder uri, Operator op, bool regex)
    {
        if (op is Operator.Hash)
            uri.Append('#');
        else if (op is Operator.Dot)
            uri.Append('.');
        else if (op is Operator.Slash)
            uri.Append('/');
        else if (op is Operator.Semicolon)
            uri.Append(';');
        else if (op is Operator.QuestionMark)
        {
            if (regex)
                uri.Append('\\');

            uri.Append('?');
        }
        else if (op is Operator.Ampersand)
            uri.Append('&');
    }

    private static void AppendSeparator(this StringBuilder uri, Operator op)
    {
        if (op is Operator.Dot)
            uri.Append('.');
        else if (op is Operator.Slash)
            uri.Append('/');
        else if (op is Operator.Semicolon)
            uri.Append(';');
        else if (op is Operator.QuestionMark or Operator.Ampersand)
            uri.Append('&');
        else
            uri.Append(',');
    }

    private static void AppendValue(this StringBuilder uri, Operator op, StringBuilder token, object value, int maxTokenLength, bool escape)
    {
        if (op is Operator.QuestionMark or Operator.Ampersand)
        {
            uri.Append(token);
            uri.Append('=');
            uri.AppendEscapedValue(char.MinValue, value, maxTokenLength, true, escape);
        }
        else if (op is Operator.Semicolon)
        {
            uri.Append(token);
            uri.AppendEscapedValue('=', value, maxTokenLength, true, escape);
        }
        else if (op is Operator.Plus or Operator.Hash)
            uri.AppendEscapedValue(char.MinValue, value, maxTokenLength, false, escape);
        else if (op is Operator.Dot or Operator.Slash or Operator.NoOp)
            uri.AppendEscapedValue(char.MinValue, value, maxTokenLength, true, escape);
    }

    private static void AppendList(this StringBuilder uri, Operator op, StringBuilder token, IList list, int maxTokenLength, bool composite, bool regex)
    {
        var first = true;
        foreach (var element in list)
        {
            if (first)
            {
                uri.AppendValue(op, token, element, maxTokenLength, regex);
                first = false;
            }
            else
            {
                if (composite)
                {
                    uri.AppendSeparator(op);
                    uri.AppendValue(op, token, element, maxTokenLength, regex);
                }
                else
                {
                    uri.Append(',');
                    uri.AppendElementValue(op, element, maxTokenLength, regex);
                }
            }
        }
    }

    private static void AppendDictionary(this StringBuilder uri, Operator op, StringBuilder token, IDictionary dictionary, int maxTokenLength, bool composite, bool regex)
    {
        var first = true;
        if (maxTokenLength != -1)
            throw new InvalidOperationException("Value trimming is not allowed on dictionaries");

        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Value is null)
                throw new InvalidOperationException("Null value is not allowed in dictionaries");

            if (composite)
            {
                if (!first)
                    uri.AppendSeparator(op);

                uri.AppendElementValue(op, (string)entry.Key, maxTokenLength, regex);
                uri.Append('=');
            }
            else
            {
                if (!first)
                {
                    uri.Append(',');
                    uri.AppendElementValue(op, (string)entry.Key, maxTokenLength, regex);
                }
                else
                    uri.AppendValue(op, token, (string)entry.Key, maxTokenLength, regex);

                uri.Append(',');
            }

            uri.AppendElementValue(op, entry.Value, maxTokenLength, regex);
            first = false;
        }
    }

    private static void AppendElementValue(this StringBuilder uri, Operator op, object value, int maxTokenLength, bool regex)
    {
        if (op is Operator.Plus or Operator.Hash)
            uri.AppendEscapedValue(char.MinValue, value, maxTokenLength, false, regex);
        else if (op is Operator.QuestionMark or Operator.Ampersand or Operator.Semicolon or Operator.Dot or Operator.Slash or Operator.NoOp)
            uri.AppendEscapedValue(char.MinValue, value, maxTokenLength, true, regex);
    }

    private static void AppendEscapedValue(this StringBuilder uri, char prefix, object value, int maxTokenLength, bool replaceReserved, bool regex)
    {
        var formatted = Format(value).AsSpan();
        var length = maxTokenLength is not -1 ? Math.Min(maxTokenLength, formatted.Length) : formatted.Length;
        var insideReserved = false;
        var reservedBuffer = new StringBuilder(3);
        var runeBuffer = (Span<char>)stackalloc char[2];
        var buffer = (Span<char>)stackalloc char[12];
        var bufferIndex = 0;

        uri.EnsureCapacity(length * 2);
        if (length > 0 && prefix is not char.MinValue)
            uri.Append(prefix);

        if (regex)
        {
            uri.Append(formatted.Length > length ? formatted[..length] : formatted);
            return;
        }

        for (var index = 0; index < length; index++)
        {
            var character = formatted[index];
            if (character is '%' && !replaceReserved)
            {
                insideReserved = true;
                reservedBuffer.Clear();
            }

            if (char.IsSurrogate(character))
            {
                Rune.DecodeFromUtf16(formatted[index++..(index + 1)], out var rune, out _);
                rune.EncodeToUtf16(runeBuffer);
                Uri.TryEscapeDataString(runeBuffer, buffer[bufferIndex..], out var written);
                bufferIndex += written;
            }
            else if (replaceReserved || !IsUnreserved(character))
            {
                Uri.TryEscapeDataString(formatted[index..(index + 1)], buffer[bufferIndex..], out var written);
                bufferIndex += written;
            }
            else
                buffer[bufferIndex++] = character;

            if (insideReserved)
            {
                reservedBuffer.Append(buffer[..bufferIndex]);

                if (reservedBuffer.Length is 3)
                {
                    var isEscaped = reservedBuffer[0] is '%' &&
                                    char.IsAsciiHexDigit(reservedBuffer[1]) &&
                                    char.IsAsciiHexDigit(reservedBuffer[2]);

                    if (!isEscaped)
                    {
                        uri.Append("%25");
                        Uri.TryEscapeDataString(reservedBuffer.ToString(1, 2), buffer, out var written);
                        uri.Append(buffer[..written]);
                    }
                    else
                        uri.Append(reservedBuffer);

                    insideReserved = false;
                    reservedBuffer.Clear();
                }
            }
            else
            {
                if (character is ' ')
                    uri.Append("%20");
                else if (character is '%')
                    uri.Append("%25");
                else
                    uri.Append(buffer[..bufferIndex]);
            }

            bufferIndex = 0;
        }

        if (insideReserved)
        {
            uri.Append("%25");
            uri.Append(reservedBuffer.ToString(1, reservedBuffer.Length - 1));
        }
    }

    private static bool IsEmpty([NotNullWhen(false)] object? value) => value switch
    {
        null => true,
        string or bool or int or long or float or double or decimal => false,
        IList list => list.Count is 0,
        IDictionary dictionary => dictionary.Count is 0,
        _ => true
    };

    private static ReadOnlySpan<char> Rfc2396UnreservedMarks => "-_.~*'()!";
    private static bool IsUnreserved(char character) => char.IsAsciiLetterOrDigit(character) ||
                                                        Rfc2396UnreservedMarks.Contains(character);

    private static string Format(object value) => value switch
    {
        string str => str,
        bool boolean => boolean ? "true" : "false",
        int number => number.ToString(CultureInfo.InvariantCulture),
        long number => number.ToString(CultureInfo.InvariantCulture),
        float number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        _ => throw new ArgumentException($"Type {value.GetType().Name} is not formattable", nameof(value))
    };
}