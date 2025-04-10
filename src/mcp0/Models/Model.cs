using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Configuration))]
[JsonSerializable(typeof(Server))]
[JsonSerializable(typeof(StdioServer))]
[JsonSerializable(typeof(SseServer))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class Model : JsonSerializerContext
{
    public static async Task<Configuration> Load(string[] paths, CancellationToken cancellationToken)
    {
        var merged = new Configuration();

        var tasks = new Task<Configuration>[paths.Length];
        for (var index = 0; index < paths.Length; index++)
            tasks[index] = Load(paths[index], cancellationToken);

        var configurations = await Task.WhenAll(tasks);
        foreach (var configuration in configurations)
            merged.Merge(configuration);

        return merged;
    }

    public static async Task<Configuration> Load(string path, CancellationToken cancellationToken)
    {
        Configuration? configuration;
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            configuration = await JsonSerializer.DeserializeAsync(stream, Default.Configuration, cancellationToken);

        if (configuration is null)
            throw new InvalidOperationException("Configuration is empty");

        return configuration;
    }
}