using System.Text.RegularExpressions;

using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal static partial class PromptTemplate
{
    [GeneratedRegex(@"\{\{(?<name>[a-zA-Z_][a-zA-Z0-9_]+)(?<required>\??):?(?<description>[^\}]*)\}\}", RegexOptions.Compiled)]
    private static partial Regex GenerateEngine();
    private static readonly Regex engine = GenerateEngine();

    public static List<PromptArgument> Parse(string template)
    {
        return engine.Matches(template).Select(ToArgument).ToList();

        static PromptArgument ToArgument(Match match)
        {
            return new()
            {
                Name = match.Groups["name"].Value,
                Description = match.Groups["description"].Length is 0 ? null : match.Groups["description"].Value,
                Required = match.Groups["required"].Length is 0 ? true : null
            };
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