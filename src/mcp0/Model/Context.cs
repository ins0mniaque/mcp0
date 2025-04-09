using System.Text.Json;
using System.Text.Json.Serialization;

using ModelContextProtocol;

namespace mcp0.Model;

internal sealed class Context
{
    [JsonPropertyName("servers")]
    public Dictionary<string, Server>? Servers { get; set; }

    public void Merge(Context config)
    {
        if (config.Servers is { } servers)
        {
            Servers ??= new(servers.Count, StringComparer.Ordinal);
            foreach (var entry in servers)
                Servers[entry.Key] = entry.Value;
        }
    }

    public static async Task<Context> Read(string[] paths, CancellationToken cancellationToken)
    {
        var merged = new Context();

        var tasks = new Task<Context>[paths.Length];
        for (var index = 0; index < paths.Length; index++)
            tasks[index] = Read(paths[index], cancellationToken);

        var configs = await Task.WhenAll(tasks);
        foreach (var config in configs)
            merged.Merge(config);

        if (merged.Servers is null)
            throw new InvalidOperationException("missing context servers configuration");

        return merged;
    }

    public static async Task<Context> Read(string path, CancellationToken cancellationToken)
    {
        Context? contextConfig;
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            contextConfig = await JsonSerializer.DeserializeAsync(stream, Model.Default.Context, cancellationToken);

        if (contextConfig is null)
            throw new InvalidOperationException("context is empty");

        return contextConfig;
    }

    public McpServerConfig[] ToMcpServerConfigs()
    {
        return Servers?.Select(static entry => entry.Value.ToMcpServerConfig(entry.Key)).ToArray() ?? [];
    }
}