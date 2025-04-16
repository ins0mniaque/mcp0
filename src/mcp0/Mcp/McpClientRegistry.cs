using System.Collections;

using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace mcp0.Mcp;

internal class McpClientRegistry<T>(string itemType, Func<T, string> keySelector) : IEnumerable<T>
{
    protected readonly Dictionary<string, (IMcpClient Client, T Item)> registry = new(StringComparer.Ordinal);

    public int Count => registry.Count;

    public (IMcpClient Client, T Item) Find(string? key)
    {
        if (TryFind(key) is not { } found)
            throw NotFoundException(key);

        return found;
    }

    public (IMcpClient Client, T Item)? TryFind(string? key)
    {
        if (key is null || !registry.TryGetValue(key, out var found))
            return null;

        return found;
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
            var clientItems = clientsItems[index];
            foreach (var clientItem in clientItems)
                registry[keySelector(clientItem)] = (client, clientItem);
        }
    }

    internal virtual void Clear() => registry.Clear();

    public IEnumerator<T> GetEnumerator() => registry.Select(static entry => entry.Value.Item).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected McpException NotFoundException(string? key)
    {
        if (key is null)
            throw new McpException($"Missing {itemType} request parameter");

        return new McpException($"Unknown {itemType}: '{key}'");
    }
}