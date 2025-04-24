using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models.Converters;

internal sealed class ToolsConverter : KeyedListConverter<Tool>
{
    protected override JsonTypeInfo<Tool> JsonTypeInfo => ModelContext.Default.Tool;

    protected override Func<Tool, string?> GetKey { get; } = static tool => tool.Name;
    protected override Action<Tool, string?> SetKey { get; } = static (tool, name) => tool.Name = name!;
}