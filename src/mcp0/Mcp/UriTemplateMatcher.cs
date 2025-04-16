using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace mcp0.Mcp;

internal sealed class UriTemplateMatcher
{
    private static readonly RouteValueDictionary defaults = new();

    private readonly TemplateMatcher matcher;
    private readonly RouteValueDictionary values = new();

    public UriTemplateMatcher(Uri uriTemplate)
    {
        var path = uriTemplate.LocalPath;
        var template = TemplateParser.Parse(path);

        matcher = new TemplateMatcher(template, defaults);
        UriTemplate = uriTemplate;
    }

    public Uri UriTemplate { get; }

    public bool Match(Uri uri)
    {
        if (UriTemplate.Scheme != uri.Scheme || UriTemplate.Authority != uri.Authority)
            return false;

        return matcher.TryMatch(uri.LocalPath, values);
    }
}