using System.Text.RegularExpressions;

namespace mcp0.Core;

internal static partial class Template
{
    [GeneratedRegex(@"\{\{(?<name>[a-zA-Z_][a-zA-Z0-9_]+)(?<required>\??):?(?<type>[a-z]*)#?(?<description>[^\}]*)\}\}",
        RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GenerateParser();
    private static readonly Regex parser = GenerateParser();

    public static IEnumerable<T> Parse<T>(string template, Func<string, string?, string?, bool, T> factory)
    {
        return parser.Matches(template).Select(CreateArgument);

        T CreateArgument(Match match) => factory(
            match.Groups["name"].Value,
            match.Groups["type"].Length is 0 ? null : match.Groups["type"].Value,
            match.Groups["description"].Length is 0 ? null : match.Groups["description"].Value,
            match.Groups["required"].Length is 0);
    }

    public static string Render<T>(string template, IReadOnlyDictionary<string, T> arguments)
    {
        return parser.Replace(template, ReplaceArgument);

        string ReplaceArgument(Match match)
        {
            var name = match.Groups["name"].Value;
            if (arguments.TryGetValue(name, out var value))
                return value?.ToString() ?? string.Empty;

            if (match.Groups["required"].Length is 0)
                return match.Value;

            return string.Empty;
        }
    }
}