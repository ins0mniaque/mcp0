using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Configuration;

internal sealed class ContextConfig
{
    [JsonPropertyName("servers")]
    public Dictionary<string, ServerConfig>? Servers { get; set; }

    public void Merge(ContextConfig config)
    {
        if (config.Servers is { } servers)
        {
            Servers ??= new(servers.Count, StringComparer.Ordinal);
            foreach (var entry in servers)
                Servers[entry.Key] = entry.Value;
        }
    }

    public static async Task<ContextConfig> Read(string[] paths, CancellationToken cancellationToken)
    {
        var merged = new ContextConfig();

        var tasks = new Task<ContextConfig>[paths.Length];
        for (var index = 0; index < paths.Length; index++)
            tasks[index] = Read(paths[index], cancellationToken);

        var configs = await Task.WhenAll(tasks);
        foreach (var config in configs)
            merged.Merge(config);

        if (merged.Servers is null)
            throw new InvalidOperationException("missing context servers configuration");

        return merged;
    }

    public static async Task<ContextConfig> Read(string path, CancellationToken cancellationToken)
    {
        ContextConfig? contextConfig;
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            contextConfig = await JsonSerializer.DeserializeAsync(stream, SerializerContext.Default.ContextConfig, cancellationToken);

        if (contextConfig is null)
            throw new InvalidOperationException("context is empty");

        return contextConfig;
    }
}