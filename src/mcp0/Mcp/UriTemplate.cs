using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace mcp0.Mcp;

internal sealed class UriTemplate
{
    private readonly Regex parser;

    public UriTemplate(string template)
    {
        Template = template;
        parser = CreateParser(template, RegexOptions.Compiled);
    }

    public string Template { get; }

    public bool Match(string uri) => parser.Match(uri).Success;
    public IReadOnlyDictionary<string, object?>? Parse(string uri) => Parse(parser, uri);
    public string Expand(IReadOnlyDictionary<string, object?> values) => Expand(Template, values);

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
        return Expand(template, (_, token) => values.GetValueOrDefault(token), true);
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
                                         static group => (object?)group.Value);
    }

    private static Regex CreateParser(string template, RegexOptions options)
    {
        var pattern = Expand(template, static (op, token) => $"(?<{token}>[^{GetParserSeparator(op)}]*)", false);

        return new Regex(string.Concat('^', pattern, '$'), options, TimeSpan.FromSeconds(1));
    }

    private static string GetParserSeparator(Operator op) => op switch
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

    private static void ValidateLiteral(char character, int column)
    {
        if (character is '+' or '#' or '/' or ';' or '?' or '&' or ' ' or '!' or '=' or '$' or '|' or '*' or ':' or '~' or '-')
            throw new FormatException($"Invalid character '{character}' in token at column {column}");
    }

    private static int GetMaxTokenLength(StringBuilder buffer, int column)
    {
        if (buffer.Length is 0)
            return -1;

        var maxTokenLength = buffer.ToString();
        if (int.TryParse(maxTokenLength, CultureInfo.InvariantCulture, out var length))
            return length;

        throw new FormatException($"Invalid maximum token length '{maxTokenLength}' at column {column}");
    }

    private static Operator GetOperator(char character, StringBuilder token, int column)
    {
        if (character is '+')
            return Operator.Plus;
        if (character is '#')
            return Operator.Hash;
        if (character is '.')
            return Operator.Dot;
        if (character is '/')
            return Operator.Slash;
        if (character is ';')
            return Operator.Semicolon;
        if (character is '?')
            return Operator.QuestionMark;
        if (character is '&')
            return Operator.Ampersand;

        ValidateLiteral(character, column);
        token.Append(character);
        return Operator.NoOp;
    }

    private static string Expand(string uri, Func<Operator, string, object?> replaceToken, bool escape)
    {
        var result = new StringBuilder(uri.Length * 2);

        var insideToken = false;
        var token = new StringBuilder();

        var op = Operator.None;
        var composite = false;
        var insideMaxTokenLength = false;
        var maxTokenLengthBuffer = new StringBuilder(3);
        var firstToken = true;

        for (var i = 0; i < uri.Length; i++)
        {
            var character = uri[i];

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
                    throw new FormatException($"Invalid character '{character}' in template at column {i}");

                var expanded = ExpandToken(op, token.ToString(), composite, GetMaxTokenLength(maxTokenLengthBuffer, i), firstToken, replaceToken, result, i, escape);
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
                var expanded = ExpandToken(op, token.ToString(), composite, GetMaxTokenLength(maxTokenLengthBuffer, i), firstToken, replaceToken, result, i, escape);
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
                    op = GetOperator(character, token, i);
                }
                else if (insideMaxTokenLength)
                {
                    if (char.IsDigit(character))
                        maxTokenLengthBuffer.Append(character);
                    else
                        throw new FormatException($"Invalid character '{character}' in maximum token length at column {i}");
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
                        ValidateLiteral(character, i);
                        token.Append(character);
                    }
                }
            }
            else if (escape)
                result.Append(character);
            else
                result.Append(Regex.Escape(character.ToString()));
        }

        if (!insideToken)
            return result.ToString();

        throw new FormatException("Unterminated token");
    }

    private static void AddPrefix(Operator op, StringBuilder result, bool escape)
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
            if (escape)
                result.Append('?');
            else
                result.Append("\\?");
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
            AddExpandedValue(null, value, result, maxTokenLength, true, escape);
        }
        else if (op is Operator.Semicolon)
        {
            result.Append(token);
            AddExpandedValue("=", value, result, maxTokenLength, true, escape);
        }
        else if (op is Operator.Plus or Operator.Hash)
            AddExpandedValue(null, value, result, maxTokenLength, false, escape);
        else if (op is Operator.Dot or Operator.Slash or Operator.NoOp)
            AddExpandedValue(null, value, result, maxTokenLength, true, escape);
    }

    private static void AddValueElement(Operator op, object value, StringBuilder result, int maxTokenLength, bool escape)
    {
        if (op is Operator.Plus or Operator.Hash)
            AddExpandedValue(null, value, result, maxTokenLength, false, escape);
        else if (op is Operator.QuestionMark or Operator.Ampersand or Operator.Semicolon or Operator.Dot or Operator.Slash or Operator.NoOp)
            AddExpandedValue(null, value, result, maxTokenLength, true, escape);
    }

    private static bool IsPrivateUse(char character)
    {
        return char.IsBetween(character, (char)0xE000, (char)0xF8FF);
    }

    private static bool IsUcs(char character)
    {
        return char.IsBetween(character, (char)0xA0, (char)0xD7FF)
               || char.IsBetween(character, (char)0xF900, (char)0xFDCF)
               || char.IsBetween(character, (char)0xFDF0, (char)0xFFEF);
    }

    private static void AddExpandedValue(string? prefix, object value, StringBuilder result, int maxTokenLength, bool replaceReserved, bool escape)
    {
        var stringValue = Format(value);
        var max = (maxTokenLength != -1) ? Math.Min(maxTokenLength, stringValue.Length) : stringValue.Length;
        result.EnsureCapacity(max * 2);
        var toReserved = false;
        var reservedBuffer = new StringBuilder(3);

        if (max > 0 && prefix != null)
            result.Append(prefix);

        if (!escape)
        {
            result.Append(stringValue.Length > max ? stringValue[..max] : stringValue);
            return;
        }

        for (var i = 0; i < max; i++)
        {
            var character = stringValue[i];

            if (character is '%' && !replaceReserved)
            {
                toReserved = true;
                reservedBuffer.Clear();
            }

            var toAppend = character.ToString();
            if (char.IsSurrogate(character))
                toAppend = Uri.EscapeDataString(char.ConvertFromUtf32(char.ConvertToUtf32(stringValue, i++)));
            else if (replaceReserved || IsUcs(character) || IsPrivateUse(character))
                toAppend = Uri.EscapeDataString(toAppend);

            if (toReserved)
            {
                reservedBuffer.Append(toAppend);

                if (reservedBuffer.Length is 3)
                {
                    var original = reservedBuffer.ToString();
                    var isEncoded = !original.Equals(Uri.UnescapeDataString(original));

                    if (isEncoded)
                    {
                        result.Append(reservedBuffer);
                    }
                    else
                    {
                        result.Append("%25");
                        result.Append(Uri.EscapeDataString(reservedBuffer.ToString(1, 2)));
                    }
                    toReserved = false;
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
                    result.Append(toAppend);
            }
        }

        if (toReserved)
        {
            result.Append("%25");
            result.Append(reservedBuffer.ToString(1, reservedBuffer.Length - 1));
        }
    }

    private static bool IsEmpty([NotNullWhen(false)] object? value)
    {
        if (value is null)
            return true;
        if (value is string or bool or int or long or float or double or decimal)
            return false;
        if (value is IList list)
            return list.Count is 0;
        if (value is IDictionary dictionary)
            return dictionary.Count is 0;

        return true;
    }

    private static string Format(object value)
    {
        return value switch
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

    private static bool ExpandToken(
            Operator op,
            string token,
            bool composite,
            int maxTokenLength,
            bool firstToken,
            Func<Operator, string, object?> replaceToken,
            StringBuilder result,
            int column,
            bool escape)
    {
        if (string.IsNullOrEmpty(token))
            throw new FormatException($"Empty token at column {column}");

        var value = replaceToken(op, token);
        if (IsEmpty(value))
            return false;

        if (firstToken)
            AddPrefix(op, result, escape);
        else
            AddSeparator(op, result);

        if (value is string or bool or int or long or float or double or decimal)
            AddStringValue(op, token, value, result, maxTokenLength, escape);
        else if (value is IList)
            AddListValue(op, token, (IList)value, result, maxTokenLength, composite, escape);
        else if (value is IDictionary)
            AddDictionaryValue(op, token, ((IDictionary)value), result, maxTokenLength, composite, escape);
        else
            throw new InvalidOperationException($"Invalid value type {value.GetType().Name} passed as replacement for token '{token}' at column {column}");

        return true;
    }

    private static void AddStringValue(Operator op, string token, object value, StringBuilder result, int maxTokenLength, bool escape)
    {
        AddValue(op, token, value, result, maxTokenLength, escape);
    }

    private static void AddListValue(Operator op, string token, IList value, StringBuilder result, int maxTokenLength, bool composite, bool escape)
    {
        var first = true;
        foreach (var element in value)
        {
            if (first)
            {
                AddValue(op, token, element, result, maxTokenLength, escape);
                first = false;
            }
            else
            {
                if (composite)
                {
                    AddSeparator(op, result);
                    AddValue(op, token, element, result, maxTokenLength, escape);
                }
                else
                {
                    result.Append(',');
                    AddValueElement(op, element, result, maxTokenLength, escape);
                }
            }
        }
    }

    private static void AddDictionaryValue(Operator op, string token, IDictionary value, StringBuilder result, int maxTokenLength, bool composite, bool escape)
    {
        var first = true;
        if (maxTokenLength != -1)
            throw new InvalidOperationException("Value trimming is not allowed on dictionaries");

        foreach (DictionaryEntry entry in value)
        {
            if (composite)
            {
                if (!first)
                    AddSeparator(op, result);
                AddValueElement(op, (string)entry.Key, result, maxTokenLength, escape);
                result.Append('=');
            }
            else
            {
                if (first)
                {
                    AddValue(op, token, (string)entry.Key, result, maxTokenLength, escape);
                }
                else
                {
                    result.Append(',');
                    AddValueElement(op, (string)entry.Key, result, maxTokenLength, escape);
                }
                result.Append(',');
            }

            if (entry.Value is null)
                throw new InvalidOperationException("Null value is not allowed in dictionaries");

            AddValueElement(op, entry.Value, result, maxTokenLength, escape);
            first = false;
        }
    }
}