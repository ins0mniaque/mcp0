using System.Text.Json;
using System.Text.RegularExpressions;

namespace mcp0.Core;

internal static partial class FunctionCall
{
    [GeneratedRegex(@"(?<function>[a-zA-Z][a-zA-Z0-9]*)\s*\((?<arguments>.*)\)",
        RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GenerateParser();
    private static readonly Regex parser = GenerateParser();

    public static bool TryParse(string functionCall, out string function, out JsonElement[] arguments)
    {
        function = string.Empty;
        arguments = [];

        functionCall = functionCall.Trim();
        var match = parser.Match(functionCall);
        if (!match.Success || match.Index is not 0 || match.Length != functionCall.Length)
            return false;

        function = match.Groups["function"].Value;
        if (function.Length is 0)
            return false;

        var jsonArguments = match.Groups["arguments"].Value.Trim();
        if (jsonArguments.Length > 0)
        {
            var elements = JsonSerializer.Deserialize('[' + jsonArguments + ']', JsonSchemaContext.Default.JsonElement);
            if (elements.ValueKind is not JsonValueKind.Array)
                return false;

            arguments = elements.EnumerateArray().ToArray();
        }

        return true;
    }
}