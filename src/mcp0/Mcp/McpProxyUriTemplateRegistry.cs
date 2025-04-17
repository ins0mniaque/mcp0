using System.Diagnostics.CodeAnalysis;

using ModelContextProtocol.Client;

namespace mcp0.Mcp;

internal sealed class McpProxyUriTemplateRegistry<T>(string itemType, Func<T, string> keySelector) : McpProxyRegistry<T>(itemType, keySelector) where T : notnull
{
    private readonly Dictionary<string, UriTemplateMatcher> matchers = new(StringComparer.Ordinal);

    public T Match(string? key, out IMcpClient client)
    {
        if (!TryMatch(key, out client, out var item))
            throw NotFoundException(key);

        return item;
    }

    public bool TryMatch(string? key, out IMcpClient client, [NotNullWhen(true)] out T item)
    {
        client = null!;
        item = default!;

        if (key is null)
            return false;

        var uri = new Uri(key, UriKind.Absolute);
        foreach (var entry in registry)
        {
            var matcher = GetMatcher(entry.Key);
            if (matcher.Match(uri))
            {
                client = entry.Value.Client;
                item = entry.Value.Item;
                return true;
            }
        }

        return false;
    }

    internal override void Clear()
    {
        base.Clear();
        matchers.Clear();
    }

    private UriTemplateMatcher GetMatcher(string key)
    {
        if (!matchers.TryGetValue(key, out var matcher))
            matchers[key] = matcher = new UriTemplateMatcher(new Uri(key, UriKind.Absolute));

        return matcher;
    }
}