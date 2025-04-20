using System.Text.RegularExpressions;

namespace mcp0.Mcp;

internal sealed class UriTemplate(string template)
{
    private readonly Regex parser = UriTemplateEngine.CreateParser(template, RegexOptions.Compiled);

    public bool IsMatch(string uri) => parser.IsMatch(uri);
    public IReadOnlyDictionary<string, object?>? Parse(string uri) => UriTemplateEngine.Parse(parser, uri);
    public string Render(IReadOnlyDictionary<string, object?> values) => UriTemplateEngine.Render(template, values);

    public static bool IsMatch(string template, string uri)
    {
        return UriTemplateEngine.CreateParser(template, RegexOptions.None).IsMatch(uri);
    }

    public static IReadOnlyDictionary<string, object?>? Parse(string template, string uri)
    {
        return UriTemplateEngine.Parse(UriTemplateEngine.CreateParser(template, RegexOptions.None), uri);
    }

    public static string Render(string template, IReadOnlyDictionary<string, object?> values)
    {
        return UriTemplateEngine.Render(template, values);
    }
}