using System.Text.Json;

using Generator.Equals;

using mcp0.Core;

namespace mcp0.Models;

[Equatable]
internal sealed partial record Configuration
{
    [UnorderedEquality]
    public List<Server>? Servers { get; set; }

    [UnorderedEquality]
    public List<Prompt>? Prompts { get; set; }

    [UnorderedEquality]
    public List<Resource>? Resources { get; set; }

    [UnorderedEquality]
    public List<Tool>? Tools { get; set; }

    [UnorderedEquality]
    public Dictionary<string, Patch>? Patch { get; set; }

    public void Merge(Configuration configuration)
    {
        Servers = Merge(Servers, configuration.Servers);
        Prompts = Merge(Prompts, configuration.Prompts);
        Resources = Merge(Resources, configuration.Resources);
        Tools = Merge(Tools, configuration.Tools);
        Patch = Merge(Patch, configuration.Patch);
    }

    public void Validate()
    {
        Servers?.ForEach(Server.Validate);
        Prompts?.ForEach(Prompt.Validate);
        Resources?.ForEach(Resource.Validate);
        Tools?.ForEach(Tool.Validate);
    }

    public static Configuration Parse(string[]? servers, string[]? prompts, string[]? resources, string[]? tools)
    {
        return new()
        {
            Servers = servers?.Select(Server.Parse).ToList(),
            Prompts = prompts?.Select(Prompt.Parse).ToList(),
            Resources = resources?.Select(Resource.Parse).ToList(),
            Tools = tools?.Select(Tool.Parse).ToList()
        };
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

    private static Dictionary<string, T>? Merge<T>(Dictionary<string, T>? dictionary, Dictionary<string, T>? with)
    {
        if (with is null) return dictionary;
        if (dictionary is null) return with;

        foreach (var entry in with)
            dictionary[entry.Key] = entry.Value;

        return dictionary;
    }

    private static List<T>? Merge<T>(List<T>? list, List<T>? with)
    {
        if (with is null) return list;
        if (list is null) return with;

        list.AddRange(with);

        return list;
    }
}