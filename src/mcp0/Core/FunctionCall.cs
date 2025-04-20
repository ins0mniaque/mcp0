using System.Text.Json;
using System.Text.RegularExpressions;

namespace mcp0.Core;

internal static partial class FunctionCall
{
    [GeneratedRegex(@"(?<function>[a-zA-Z_][a-zA-Z0-9_]*)\s*\((?<arguments>.*)\)",
        RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GenerateParser();
    private static readonly Regex parser = GenerateParser();
    private static ReadOnlySpan<char> Indentation => "    ";

    public static bool Match(string call)
    {
        return parser.IsMatch(call.Trim());
    }

    public static void Parse(string call, out string function, out JsonElement[] arguments)
    {
        if (TryParse(call, out function, out arguments, out var error))
            return;

        if (error is not null)
        {
            var position = (int)(error.BytePositionInLine ?? 0) + function.Length;
            var message = error.Message.Split('.', 2)[0];

            throw new FormatException($"Invalid argument: {message} at position {position}\n\n{Indentation}{call}\n{Indentation}{new string(' ', position)}^");
        }

        throw new FormatException("Invalid function call format");
    }

    public static bool TryParse(string call, out string function, out JsonElement[] arguments)
    {
        return TryParse(call, out function, out arguments, out _);
    }

    private static bool TryParse(string call, out string function, out JsonElement[] arguments, out JsonException? error)
    {
        function = string.Empty;
        arguments = [];
        error = null;

        call = call.Trim();
        var match = parser.Match(call);
        if (!match.Success || match.Index is not 0 || match.Length != call.Length)
            return false;

        function = match.Groups["function"].Value;
        if (function.Length is 0)
            return false;

        var json = match.Groups["arguments"].Value.Trim();
        if (json.Length is 0)
            return true;

        try
        {
            var elements = JsonSerializer.Deserialize('[' + json + ']', JsonSchemaContext.Default.JsonElement);

            arguments = elements.EnumerateArray().ToArray();
            return true;
        }
        catch (JsonException exception)
        {
            error = exception;
            return false;
        }
    }
}