using System.Text.Json.Serialization.Metadata;

namespace mcp0.Models.Converters;

internal sealed class ResourcesConverter : KeyedListConverter<Resource>
{
    protected override JsonTypeInfo<Resource> JsonTypeInfo => ModelContext.Default.Resource;

    protected override Func<Resource, string?> GetKey { get; } = static resource => resource.Name;
    protected override Action<Resource, string?> SetKey { get; } = static (resource, name) => resource.Name = name!;
}