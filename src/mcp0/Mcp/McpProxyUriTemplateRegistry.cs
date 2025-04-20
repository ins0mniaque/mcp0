using System.Diagnostics.CodeAnalysis;

using ModelContextProtocol.Client;

namespace mcp0.Mcp;

internal sealed class McpProxyUriTemplateRegistry<T>(string itemType, Func<T, string> keySelector, Func<T, T?>? map = null) : McpProxyRegistry<T>(itemType, keySelector, map) where T : notnull
{
    private readonly UriTemplateCache uriTemplateCache = new();

    public T Match(string? uri, out IMcpClient client)
    {
        if (!TryMatch(uri, out client, out var item))
            throw NotFoundException(uri);

        return item;
    }

    public bool TryMatch(string? uri, out IMcpClient client, [NotNullWhen(true)] out T item)
    {
        client = null!;
        item = default!;

        if (uri is null)
            return false;

        foreach (var entry in registry)
        {
            var uriTemplate = uriTemplateCache.GetUriTemplate(entry.Key);
            if (uriTemplate.IsMatch(uri))
            {
                client = entry.Value.Client;
                item = entry.Value.Item;
                return true;
            }
        }

        return false;
    }

    public string Unmap(T template, string uri)
    {
        var key = keySelector(template);
        if (!inverseMap.TryGetValue(key, out var inverseKey))
            return uri;

        var uriTemplate = uriTemplateCache.GetUriTemplate(key);
        var inverseUriTemplate = uriTemplateCache.GetUriTemplate(inverseKey);

        if (uriTemplate.Parse(uri) is { } values)
            return inverseUriTemplate.Render(values);

        return uri;
    }

    internal override void Clear()
    {
        base.Clear();
        uriTemplateCache.Clear();
    }
}