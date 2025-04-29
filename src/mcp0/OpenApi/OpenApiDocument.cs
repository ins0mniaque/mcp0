using System.Text.Json;
using System.Text.Json.Serialization;

namespace mcp0.OpenApi;

internal sealed class OpenApiDocument
{
    [JsonPropertyName("openapi")]
    public string Version { get; init; } = "3.1.0";
    public OpenApiInfo? Info { get; init; }
    public Dictionary<string, Dictionary<string, OpenApiOperation>>? Paths { get; init; }
}

internal sealed class OpenApiInfo
{
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
}

internal sealed class OpenApiOperation
{
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? OperationId { get; init; }
    public OpenApiRequestBody? RequestBody { get; init; }
    public Dictionary<string, OpenApiResponse>? Responses { get; init; }
}

internal sealed class OpenApiRequestBody
{
    public string? Description { get; set; }
    public bool Required { get; set; }
    public Dictionary<string, OpenApiMediaType>? Content { get; set; }
}

internal sealed class OpenApiMediaType
{
    public JsonElement? Schema { get; set; }
}

internal sealed class OpenApiResponse
{
    public string? Description { get; set; }
    public Dictionary<string, OpenApiMediaType>? Content { get; set; }
}