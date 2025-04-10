using System.Text.Json.Serialization;

namespace mcp0.Models;

internal sealed class Configuration
{
    [JsonPropertyName("prompts")]
    public Dictionary<string, string>? Prompts { get; set; }

    [JsonPropertyName("resources")]
    public Dictionary<string, string>? Resources { get; set; }

    [JsonPropertyName("servers")]
    public Dictionary<string, Server>? Servers { get; set; }

    public void Merge(Configuration configuration)
    {
        if (configuration.Prompts is { } prompts)
        {
            Prompts ??= new(prompts.Count, StringComparer.Ordinal);
            foreach (var entry in prompts)
                Prompts[entry.Key] = entry.Value;
        }

        if (configuration.Resources is { } resources)
        {
            Resources ??= new(resources.Count, StringComparer.Ordinal);
            foreach (var entry in resources)
                Resources[entry.Key] = entry.Value;
        }

        if (configuration.Servers is { } servers)
        {
            Servers ??= new(servers.Count, StringComparer.Ordinal);
            foreach (var entry in servers)
                Servers[entry.Key] = entry.Value;
        }
    }
}