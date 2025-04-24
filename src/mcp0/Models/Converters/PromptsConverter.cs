using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models.Converters;

internal sealed class PromptsConverter : KeyedListConverter<Prompt>
{
    protected override JsonTypeInfo<Prompt> JsonTypeInfo => ModelContext.Default.Prompt;

    protected override Func<Prompt, string?> GetKey { get; } = static prompt => prompt.Name;
    protected override Action<Prompt, string?> SetKey { get; } = static (prompt, name) => prompt.Name = name!;
}