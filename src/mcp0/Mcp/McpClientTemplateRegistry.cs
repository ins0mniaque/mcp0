using ModelContextProtocol.Client;

namespace mcp0.Mcp;

internal sealed class McpClientTemplateRegistry<T>(string itemType, Func<T, string> keySelector) : McpClientRegistry<T>(itemType, keySelector)
{
    private readonly Dictionary<Uri, UriTemplateMatcher> matchers = new();

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
        foreach (var entry in registry)
        {
            var uriTemplate = new Uri(entry.Key, UriKind.Absolute);
            var matcher = GetMatcher(uriTemplate);
            if (matcher.Match(uri))
                return entry.Value;
        }

        return null;
    }

    internal override void Clear()
    {
        base.Clear();
        matchers.Clear();
    }

    private UriTemplateMatcher GetMatcher(Uri uriTemplate)
    {
        if (!matchers.TryGetValue(uriTemplate, out var matcher))
            matchers[uriTemplate] = matcher = new UriTemplateMatcher(uriTemplate);

        return matcher;
    }
}