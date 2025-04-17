using System.Diagnostics.CodeAnalysis;

using ModelContextProtocol.Client;

namespace mcp0.Mcp;

internal sealed class McpClientTemplateRegistry<T>(string itemType, Func<T, string> keySelector) : McpClientRegistry<T>(itemType, keySelector) where T : notnull
{
    private readonly Dictionary<Uri, UriTemplateMatcher> matchers = new();

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
            var uriTemplate = new Uri(entry.Key, UriKind.Absolute);
            var matcher = GetMatcher(uriTemplate);
            if (matcher.Match(uri))
            {
                client = entry.Value.Client;
                item = entry.Value.Item;
                return true;
            }
        }

        return false;
    }

    private bool TryMatch2(Uri uri, out IMcpClient client, [NotNullWhen(true)] out T item)
    {
        foreach (var entry in registry)
        {
            var uriTemplate = new Uri(entry.Key, UriKind.Absolute);
            var matcher = GetMatcher(uriTemplate);
            if (matcher.Match(uri))
            {
                client = entry.Value.Client;
                item = entry.Value.Item;
                return true;
            }
        }

        client = null!;
        item = default!;
        return false;
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