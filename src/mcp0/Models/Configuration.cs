using System.Text.Json;

using Generator.Equals;

using mcp0.Core;

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

    [UnorderedEquality]
    public Dictionary<string, Patch>? Patch { get; set; }

    public void Merge(Configuration configuration)
    {
        Prompts = Dictionary.Merge(Prompts, configuration.Prompts);
        Resources = Dictionary.Merge(Resources, configuration.Resources);
        Tools = Dictionary.Merge(Tools, configuration.Tools);
        Servers = Dictionary.Merge(Servers, configuration.Servers);
        Patch = Dictionary.Merge(Patch, configuration.Patch);
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
        await using (var stream = new FileStream(Posix.ExpandPath(path), FileMode.Open, FileAccess.Read))
            configuration = await JsonSerializer.DeserializeAsync(stream, ModelContext.Default.Configuration, cancellationToken);

        if (configuration is null)
            throw new InvalidOperationException("Configuration is empty");

        return configuration;
    }
}