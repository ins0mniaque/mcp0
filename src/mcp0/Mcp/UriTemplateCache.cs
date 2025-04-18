namespace mcp0.Mcp;

internal sealed class UriTemplateCache
{
    private readonly Dictionary<string, UriTemplate> matchers = new(StringComparer.Ordinal);

    public UriTemplate GetUriTemplate(string uriTemplate)
    {
        if (!matchers.TryGetValue(uriTemplate, out var matcher))
            matchers[uriTemplate] = matcher = new UriTemplate(uriTemplate);

        return matcher;
    }

    public void Clear() => matchers.Clear();
}