using System.Text.Json;
using System.Text.Json.Serialization;

using ModelContextProtocol;

namespace mcp0.Model;

internal sealed class Configuration
{
    [JsonPropertyName("servers")]
    public Dictionary<string, Server>? Servers { get; set; }

    public void Merge(Configuration configuration)
    {
        if (configuration.Servers is { } servers)
        {
            Servers ??= new(servers.Count, StringComparer.Ordinal);
            foreach (var entry in servers)
                Servers[entry.Key] = entry.Value;
        }
    }

    public static async Task<Configuration> Read(string[] paths, CancellationToken cancellationToken)
    {
        var merged = new Configuration();

        var tasks = new Task<Configuration>[paths.Length];
        for (var index = 0; index < paths.Length; index++)
            tasks[index] = Read(paths[index], cancellationToken);

        var configs = await Task.WhenAll(tasks);
        foreach (var config in configs)
            merged.Merge(config);

        if (merged.Servers is null)
            throw new InvalidOperationException("Servers configuration is empty");

        return merged;
    }

    public static async Task<Configuration> Read(string path, CancellationToken cancellationToken)
    {
        Configuration? configuration;
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            configuration = await JsonSerializer.DeserializeAsync(stream, Model.Default.Configuration, cancellationToken);

        if (configuration is null)
            throw new InvalidOperationException("Configuration is empty");

        return configuration;
    }

    public McpServerConfig[] ToMcpServerConfigs()
    {
        return Servers?.Select(static entry => entry.Value.ToMcpServerConfig(entry.Key)).ToArray() ?? [];
    }
}