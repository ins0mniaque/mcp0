using System.Collections;
using System.Diagnostics.CodeAnalysis;

using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace mcp0.Mcp;

internal class McpProxyRegistry<T>(string itemType, Func<T, string> keySelector, Func<T, T?>? map = null) : IEnumerable<T> where T : notnull
{
    protected readonly Dictionary<string, (IMcpClient Client, T Item)> registry = new(StringComparer.Ordinal);
    protected readonly Dictionary<string, string> inverseMap = new(StringComparer.Ordinal);
    protected readonly Func<T, string> keySelector = keySelector;

    public int Count => registry.Count;

    public T Find(string? key, out IMcpClient client)
    {
        if (!TryFind(key, out client, out var item))
            throw NotFoundException(key);

        return item;
    }

    public bool TryFind(string? key, out IMcpClient client, [NotNullWhen(true)] out T item)
    {
        client = null!;
        item = default!;

        if (key is null || !registry.TryGetValue(key, out var found))
            return false;

        client = found.Client;
        item = found.Item;
        return true;
    }

    public string Unmap(T item)
    {
        var key = keySelector(item);

        return inverseMap.GetValueOrDefault(key, key);
    }

    internal async Task Register(IReadOnlyList<IMcpClient> clients, Func<IMcpClient, Task<IList<T>>> task)
    {
        var tasks = new List<Task<IList<T>>>(clients.Count);
        foreach (var client in clients)
            tasks.Add(task(client));

        var clientsItems = await Task.WhenAll(tasks);
        for (var index = 0; index < clientsItems.Length; index++)
        {
            var client = clients[index];
            var items = clientsItems[index];
            foreach (var item in items)
            {
                var key = keySelector(item);
                if (map is null)
                {
                    registry[key] = (client, item);
                    continue;
                }

                if (map(item) is not { } mappedItem)
                    continue;

                var mappedKey = keySelector(mappedItem);
                if (!string.Equals(mappedKey, key, StringComparison.Ordinal))
                    inverseMap[mappedKey] = key;

                registry[mappedKey] = (client, mappedItem);
            }
        }
    }

    internal virtual void Clear()
    {
        registry.Clear();
        inverseMap.Clear();
    }

    public IEnumerable<T> this[IMcpClient client] => registry.Where(entry => entry.Value.Client == client)
                                                             .Select(static entry => entry.Value.Item);

    public IEnumerator<T> GetEnumerator() => registry.Select(static entry => entry.Value.Item).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected McpException NotFoundException(string? key)
    {
        if (key is null)
            throw new McpException($"Missing {itemType} request parameter");

        return new McpException($"Unknown {itemType}: '{key}'");
    }
}