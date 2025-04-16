using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

using ModelContextProtocol.Client;

namespace mcp0.Mcp;

internal sealed class McpClientTemplateRegistry<T>(string itemType, Func<T, string> keySelector) : McpClientRegistry<T>(itemType, keySelector)
{
    private readonly Dictionary<Uri, TemplateMatcher> matchers = new();

    public (IMcpClient Client, T Item) Match(string? key)
    {
        if (TryMatch(key) is not { } match)
            throw NotFoundException(key);

        return match;
    }

    public (IMcpClient Client, T Item)? TryMatch(string? key)
    {
        if (key is null)
            return null;

        var uri = new Uri(key, UriKind.Absolute);
        var path = uri.LocalPath;
        var values = new RouteValueDictionary();
        foreach (var entry in registry)
        {
            var templateUri = new Uri(entry.Key, UriKind.Absolute);
            if (templateUri.Scheme != uri.Scheme || templateUri.Authority != uri.Authority)
                continue;

            var matcher = GetMatcher(templateUri);
            if (matcher.TryMatch(path, values))
                return entry.Value;
        }

        return null;
    }

    internal override void Clear()
    {
        base.Clear();
        matchers.Clear();
    }

    private TemplateMatcher GetMatcher(Uri uri)
    {
        if (!matchers.TryGetValue(uri, out var matcher))
            matchers[uri] = matcher = McpClientTemplateMatcher.Create(uri);

        return matcher;
    }
}