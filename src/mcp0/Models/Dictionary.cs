namespace mcp0.Models;

internal static class Dictionary
{
    public static Dictionary<string, T>? Merge<T>(Dictionary<string, T>? dictionary, Dictionary<string, T>? with)
    {
        if (with is null)
            return dictionary;

        if (dictionary is null)
            return with;

        foreach (var entry in with)
            dictionary[entry.Key] = entry.Value;

        return dictionary;
    }
}