using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace mcp0.Mcp;

internal sealed class UriTemplate(string template)
{
    private readonly Regex parser = CreateParser(template, RegexOptions.Compiled);

    public bool Match(string uri) => parser.Match(uri).Success;
    public IReadOnlyDictionary<string, object?>? Parse(string uri) => Parse(parser, uri);
    public string Expand(IReadOnlyDictionary<string, object?> values) => Expand(template, values);

    public static bool Match(string template, string uri)
    {
        return CreateParser(template, RegexOptions.None).Match(uri).Success;
    }

    public static IReadOnlyDictionary<string, object?>? Parse(string template, string uri)
    {
        return Parse(CreateParser(template, RegexOptions.None), uri);
    }

    public static string Expand(string template, IReadOnlyDictionary<string, object?> values)
    {
        return Expand(template, (_, token) => values.GetValueOrDefault(token), regex: false);
    }

    private static Dictionary<string, object?>? Parse(Regex parser, string uri)
    {
        var match = parser.Match(uri);
        if (!match.Success)
            return null;

        // TODO: Parse lists/dictionaries (e.g. "a,b,c" or "a=1,b=2,c=3")
        return match.Groups.Cast<Group>()
                           .Where(static group => group.Name is not "0")
                           .ToDictionary(static group => group.Name,
                                         static group => (object?)group.Value,
                                         StringComparer.Ordinal);
    }

    private static Regex CreateParser(string template, RegexOptions options)
    {
        var pattern = Expand(template, static (op, token) => $"(?<{token}>[^{GetParserSeparators(op)}]*)", regex: true);

        return new Regex(string.Concat('^', pattern, '$'), options, TimeSpan.FromSeconds(1));
    }

    private static string GetParserSeparators(Operator op) => op switch
    {
        Operator.QuestionMark or Operator.Ampersand => "&",
        _ => "/?"
    };

    private enum Operator
    {
        None,
        NoOp,
        Plus,
        Hash,
        Dot,
        Slash,
        Semicolon,
        QuestionMark,
        Ampersand
    }

    private static void ValidateLiteral(char character, int position)
    {
        if (character is '+' or '#' or '/' or ';' or '?' or '&' or ' ' or '!' or '=' or '$' or '|' or '*' or ':' or '~' or '-')
            throw new FormatException($"Invalid character '{character}' in token at position {position}");
    }

    private static int GetMaxTokenLength(StringBuilder buffer, int position)
    {
        if (buffer.Length is 0)
            return -1;

        var maxTokenLength = buffer.ToString();
        if (int.TryParse(maxTokenLength, CultureInfo.InvariantCulture, out var length))
            return length;

        throw new FormatException($"Invalid maximum token length '{maxTokenLength}' at position {position}");
    }

    private static Operator GetOperator(char character, StringBuilder token, int position)
    {
        if (character is '+') return Operator.Plus;
        if (character is '#') return Operator.Hash;
        if (character is '.') return Operator.Dot;
        if (character is '/') return Operator.Slash;
        if (character is ';') return Operator.Semicolon;
        if (character is '?') return Operator.QuestionMark;
        if (character is '&') return Operator.Ampersand;

        ValidateLiteral(character, position);
        token.Append(character);
        return Operator.NoOp;
    }

    private static string Expand(string uri, Func<Operator, string, object?> replaceToken, bool regex)
    {
        var result = new StringBuilder(uri.Length * 2);
        var maxTokenLengthBuffer = new StringBuilder(3);
        var insideMaxTokenLength = false;

        var token = new StringBuilder();
        var insideToken = false;
        var firstToken = true;
        var op = Operator.None;
        var composite = false;

        for (var index = 0; index < uri.Length; index++)
        {
            var character = uri[index];

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

                var expanded = ExpandToken(op, token.ToString(), composite, GetMaxTokenLength(maxTokenLengthBuffer, index), firstToken, replaceToken, result, index, regex);
                if (expanded && firstToken)
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
                var expanded = ExpandToken(op, token.ToString(), composite, GetMaxTokenLength(maxTokenLengthBuffer, index), firstToken, replaceToken, result, index, regex);
                if (expanded && firstToken)
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
                    op = GetOperator(character, token, index);
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
                    {
                        composite = true;
                    }
                    else
                    {
                        ValidateLiteral(character, index);
                        token.Append(character);
                    }
                }
            }
            else if (regex)
                result.Append(Regex.Escape(character.ToString()));
            else
                result.Append(character);
        }

        if (!insideToken)
            return result.ToString();

        throw new FormatException("Unterminated token");
    }

    private static void AddPrefix(Operator op, StringBuilder result, bool regex)
    {
        if (op is Operator.Hash)
            result.Append('#');
        else if (op is Operator.Dot)
            result.Append('.');
        else if (op is Operator.Slash)
            result.Append('/');
        else if (op is Operator.Semicolon)
            result.Append(';');
        else if (op is Operator.QuestionMark)
        {
            if (regex)
                result.Append('\\');

            result.Append('?');
        }
        else if (op is Operator.Ampersand)
            result.Append('&');
    }

    private static void AddSeparator(Operator op, StringBuilder result)
    {
        if (op is Operator.Dot)
            result.Append('.');
        else if (op is Operator.Slash)
            result.Append('/');
        else if (op is Operator.Semicolon)
            result.Append(';');
        else if (op is Operator.QuestionMark or Operator.Ampersand)
            result.Append('&');
        else
            result.Append(',');
    }

    private static void AddValue(Operator op, string token, object value, StringBuilder result, int maxTokenLength, bool escape)
    {
        if (op is Operator.QuestionMark or Operator.Ampersand)
        {
            result.Append(token);
            result.Append('=');
            AddExpandedValue(char.MinValue, value, result, maxTokenLength, true, escape);
        }
        else if (op is Operator.Semicolon)
        {
            result.Append(token);
            AddExpandedValue('=', value, result, maxTokenLength, true, escape);
        }
        else if (op is Operator.Plus or Operator.Hash)
            AddExpandedValue(char.MinValue, value, result, maxTokenLength, false, escape);
        else if (op is Operator.Dot or Operator.Slash or Operator.NoOp)
            AddExpandedValue(char.MinValue, value, result, maxTokenLength, true, escape);
    }

    private static void AddValueElement(Operator op, object value, StringBuilder result, int maxTokenLength, bool regex)
    {
        if (op is Operator.Plus or Operator.Hash)
            AddExpandedValue(char.MinValue, value, result, maxTokenLength, false, regex);
        else if (op is Operator.QuestionMark or Operator.Ampersand or Operator.Semicolon or Operator.Dot or Operator.Slash or Operator.NoOp)
            AddExpandedValue(char.MinValue, value, result, maxTokenLength, true, regex);
    }

    private static ReadOnlySpan<char> Rfc2396UnreservedMarks => "-_.~*'()!";
    private static bool IsUnreserved(char character) => char.IsAsciiLetterOrDigit(character) ||
                                                        Rfc2396UnreservedMarks.Contains(character);

    private static void AddExpandedValue(char prefix, object value, StringBuilder result, int maxTokenLength, bool replaceReserved, bool regex)
    {
        var formatted = Format(value).AsSpan();
        var length = maxTokenLength is not -1 ? Math.Min(maxTokenLength, formatted.Length) : formatted.Length;
        var insideReserved = false;
        var reservedBuffer = new StringBuilder(3);
        var runeBuffer = (Span<char>)stackalloc char[2];
        var buffer = (Span<char>)stackalloc char[12];
        var bufferIndex = 0;

        result.EnsureCapacity(length * 2);
        if (length > 0 && prefix is not char.MinValue)
            result.Append(prefix);

        if (regex)
        {
            result.Append(formatted.Length > length ? formatted[..length] : formatted);
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
                        result.Append("%25");
                        Uri.TryEscapeDataString(reservedBuffer.ToString(1, 2), buffer, out var written);
                        result.Append(buffer[..written]);
                    }
                    else
                        result.Append(reservedBuffer);

                    insideReserved = false;
                    reservedBuffer.Clear();
                }
            }
            else
            {
                if (character is ' ')
                    result.Append("%20");
                else if (character is '%')
                    result.Append("%25");
                else
                    result.Append(buffer[..bufferIndex]);
            }

            bufferIndex = 0;
        }

        if (insideReserved)
        {
            result.Append("%25");
            result.Append(reservedBuffer.ToString(1, reservedBuffer.Length - 1));
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

    private static bool ExpandToken(Operator op, string token, bool composite, int maxTokenLength, bool firstToken,
                                    Func<Operator, string, object?> replaceToken, StringBuilder result,
                                    int position, bool regex)
    {
        if (string.IsNullOrEmpty(token))
            throw new FormatException($"Empty token at position {position}");

        var value = replaceToken(op, token);
        if (IsEmpty(value))
            return false;

        if (firstToken)
            AddPrefix(op, result, regex);
        else
            AddSeparator(op, result);

        if (value is string or bool or int or long or float or double or decimal)
            AddStringValue(op, token, value, result, maxTokenLength, regex);
        else if (value is IList list)
            AddListValue(op, token, list, result, maxTokenLength, composite, regex);
        else if (value is IDictionary dictionary)
            AddDictionaryValue(op, token, dictionary, result, maxTokenLength, composite, regex);
        else
            throw new InvalidOperationException($"Invalid value type {value.GetType().Name} passed as replacement for token '{token}' at position {position}");

        return true;
    }

    private static void AddStringValue(Operator op, string token, object value, StringBuilder result, int maxTokenLength, bool regex)
    {
        AddValue(op, token, value, result, maxTokenLength, regex);
    }

    private static void AddListValue(Operator op, string token, IList value, StringBuilder result, int maxTokenLength, bool composite, bool regex)
    {
        var first = true;
        foreach (var element in value)
        {
            if (first)
            {
                AddValue(op, token, element, result, maxTokenLength, regex);
                first = false;
            }
            else
            {
                if (composite)
                {
                    AddSeparator(op, result);
                    AddValue(op, token, element, result, maxTokenLength, regex);
                }
                else
                {
                    result.Append(',');
                    AddValueElement(op, element, result, maxTokenLength, regex);
                }
            }
        }
    }

    private static void AddDictionaryValue(Operator op, string token, IDictionary value, StringBuilder result, int maxTokenLength, bool composite, bool regex)
    {
        var first = true;
        if (maxTokenLength != -1)
            throw new InvalidOperationException("Value trimming is not allowed on dictionaries");

        foreach (DictionaryEntry entry in value)
        {
            if (entry.Value is null)
                throw new InvalidOperationException("Null value is not allowed in dictionaries");

            if (composite)
            {
                if (!first)
                    AddSeparator(op, result);

                AddValueElement(op, (string)entry.Key, result, maxTokenLength, regex);
                result.Append('=');
            }
            else
            {
                if (!first)
                {
                    result.Append(',');
                    AddValueElement(op, (string)entry.Key, result, maxTokenLength, regex);
                }
                else
                    AddValue(op, token, (string)entry.Key, result, maxTokenLength, regex);

                result.Append(',');
            }

            AddValueElement(op, entry.Value, result, maxTokenLength, regex);
            first = false;
        }
    }
}