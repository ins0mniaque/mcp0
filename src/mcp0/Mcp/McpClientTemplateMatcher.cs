using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace mcp0.Mcp;

internal static class McpClientTemplateMatcher
{
    private static readonly RouteValueDictionary defaults = new();

    internal static TemplateMatcher Create(Uri uri)
    {
        var path = uri.LocalPath;
        var template = TemplateParser.Parse(path);

        return new TemplateMatcher(template, defaults);
    }
}