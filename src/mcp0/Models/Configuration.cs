using System.Text.Json.Serialization;

namespace mcp0.Models;

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
}