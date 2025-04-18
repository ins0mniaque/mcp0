namespace mcp0.Mcp;

internal sealed class UriTemplateCache
{
    private readonly Dictionary<string, UriTemplate> cache = new(StringComparer.Ordinal);

    public UriTemplate GetUriTemplate(string template)
    {
        if (!cache.TryGetValue(template, out var uriTemplate))
            cache[template] = uriTemplate = new UriTemplate(template);

        return uriTemplate;
    }

    public void Clear() => cache.Clear();
}