using System.Text.Json;

using Generator.Equals;

namespace mcp0.Models;

[Equatable]
internal sealed partial record Configuration
{
    [UnorderedEquality]
    public Dictionary<string, string>? Prompts { get; set; }

    [UnorderedEquality]
    public Dictionary<string, string>? Resources { get; set; }

    [UnorderedEquality]
    public Dictionary<string, string>? Tools { get; set; }

    [UnorderedEquality]
    public Dictionary<string, Server>? Servers { get; set; }

    public void Merge(Configuration configuration)
    {
        Prompts = Merge(Prompts, configuration.Prompts);
        Resources = Merge(Resources, configuration.Resources);
        Tools = Merge(Tools, configuration.Tools);
        Servers = Merge(Servers, configuration.Servers);
    }

    private static Dictionary<string, T>? Merge<T>(Dictionary<string, T>? dictionary, Dictionary<string, T>? with)
    {
        if (with is null)
            return dictionary;

        dictionary ??= new(with.Count, StringComparer.Ordinal);
        foreach (var entry in with)
            dictionary[entry.Key] = entry.Value;

        return dictionary;
    }

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
            configuration = await JsonSerializer.DeserializeAsync(stream, ModelContext.Default.Configuration, cancellationToken);

        if (configuration is null)
            throw new InvalidOperationException("Configuration is empty");

        return configuration;
    }
}