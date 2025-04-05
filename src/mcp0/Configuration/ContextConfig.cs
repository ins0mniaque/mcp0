using System.Text.Json.Serialization;

internal sealed class ContextConfig
{
    [JsonPropertyName("servers")]
    public Dictionary<string, ServerConfig>? Servers { get; set; }

    public void Merge(ContextConfig config)
    {
        if (config.Servers is { } servers)
        {
            Servers ??= new(servers.Count);
            foreach (var entry in servers)
                Servers[entry.Key] = entry.Value;
        }
    }
}
