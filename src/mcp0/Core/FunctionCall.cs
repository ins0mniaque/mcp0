using System.Text.Json;
using System.Text.RegularExpressions;

namespace mcp0.Core;

internal static partial class FunctionCall
{
    [GeneratedRegex(@"(?<function>[a-zA-Z_][a-zA-Z0-9_]*)\s*\((?<arguments>.*)\)",
        RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GenerateParser();
    private static readonly Regex parser = GenerateParser();

    public static bool TryParse(string call, out string function, out JsonElement[] arguments)
    {
        function = string.Empty;
        arguments = [];

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
            if (elements.ValueKind is not JsonValueKind.Array)
                return false;

            arguments = elements.EnumerateArray().ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}