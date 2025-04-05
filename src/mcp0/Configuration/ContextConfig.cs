using System.Text.Json.Serialization;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "<Pending>")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "<Pending>")]
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
