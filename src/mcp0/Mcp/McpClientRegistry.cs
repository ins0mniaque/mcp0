using System.Collections;

using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace mcp0.Mcp;

internal sealed class McpClientRegistry<T>(string itemType, Func<T, string> keySelector) : IEnumerable<T>
{
    private readonly Dictionary<string, (IMcpClient, T)> registry = new(StringComparer.Ordinal);

    public int Count => registry.Count;

    public (IMcpClient Client, T) Find(string? name)
    {
        if (name is null)
            throw new McpException($"Missing {itemType} name");

        if (!registry.TryGetValue(name, out var item))
            throw new McpException($"Unknown {itemType}: '{name}'");

        return item;
    }

    public (IMcpClient Client, T)? TryFind(string? name)
    {
        if (name is null || !registry.TryGetValue(name, out var item))
            return null;

        return item;
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

    internal void Clear() => registry.Clear();

    public IEnumerator<T> GetEnumerator() => registry.Select(static entry => entry.Value.Item2).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}