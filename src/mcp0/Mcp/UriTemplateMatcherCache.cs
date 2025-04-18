namespace mcp0.Mcp;

internal sealed class UriTemplateMatcherCache
{
    private readonly Dictionary<string, UriTemplateMatcher> matchers = new(StringComparer.Ordinal);

    public UriTemplateMatcher GetMatcher(string uriTemplate)
    {
        if (!matchers.TryGetValue(uriTemplate, out var matcher))
            matchers[uriTemplate] = matcher = new UriTemplateMatcher(new Uri(uriTemplate, UriKind.Absolute));

        return matcher;
    }

    public void Clear() => matchers.Clear();
}