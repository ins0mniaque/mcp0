using System.Text.Json;

internal static class Context
{
    public static async Task<ContextConfig> Load(string[] paths, CancellationToken cancellationToken)
    {
        var merged = new ContextConfig();

        var tasks = new Task<ContextConfig>[paths.Length];
        for (var index = 0; index < paths.Length; index++)
            tasks[index] = Load(paths[index], cancellationToken);

        var configs = await Task.WhenAll(tasks);
        foreach (var config in configs)
            merged.Merge(config);

        if (merged.Servers is null)
            throw new InvalidOperationException("missing context servers configuration");

        return merged;
    }

    public static async Task<ContextConfig> Load(string path, CancellationToken cancellationToken)
    {
        ContextConfig? contextConfig;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            contextConfig = await JsonSerializer.DeserializeAsync(stream, SerializerContext.Default.ContextConfig, cancellationToken);

        if (contextConfig is null)
            throw new InvalidOperationException("context is empty");

        return contextConfig;
    }
}
