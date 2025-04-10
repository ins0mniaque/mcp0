using System.Text.RegularExpressions;

namespace mcp0.Core;

internal static partial class Template
{
    [GeneratedRegex(@"\{\{(?<name>[a-zA-Z_][a-zA-Z0-9_]+)(?<required>\??):?(?<description>[^\}]*)\}\}", RegexOptions.Compiled)]
    private static partial Regex GenerateEngine();
    private static readonly Regex engine = GenerateEngine();

    internal record Argument(string Name, string? Description, bool IsRequired);

    public static Argument[] Parse(string template)
    {
        return engine.Matches(template).Select(ToArgument).ToArray();

        static Argument ToArgument(Match match)
        {
            return new(
                match.Groups["name"].Value,
                match.Groups["description"].Length is 0 ? null : match.Groups["description"].Value,
                match.Groups["required"].Length is 0);
        }
    }

    public static string Render<T>(string template, IReadOnlyDictionary<string, T> arguments)
    {
        return engine.Replace(template, ReplaceArgument);

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