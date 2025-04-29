using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Protocol.Types;

namespace mcp0.OpenApi;

internal static class OpenApiTools
{
    private static readonly TemplateBinderFactory templateBinderFactory = new ServiceCollection()
        .AddRouting()
        .BuildServiceProvider()
        .GetRequiredService<TemplateBinderFactory>();

    public static OpenApiDocument ToOpenApiDocument(this IEnumerable<Tool> tools, [StringSyntax("Route")] string toolPattern)
    {
        var routePattern = RoutePatternFactory.Parse(toolPattern);
        var templateBinder = templateBinderFactory.Create(routePattern);
        var values = new RouteValueDictionary { ["tool"] = string.Empty };

        var paths = new Dictionary<string, Dictionary<string, OpenApiOperation>>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            values["tool"] = tool.Name;
            if (templateBinder.BindValues(values) is { } path)
                paths[path] = tool.ToOpenApi();
        }

        return new()
        {
            // TODO: Proxy name/version
            Info = new() { Title = "mcp0", Description = "Description", Summary = "Summary", Version = "0.0.1" },
            Paths = paths
        };
    }

    private static readonly JsonElement unknownSchema = new JsonObject().Deserialize(OpenApiContext.Default.JsonElement);

    private static Dictionary<string, OpenApiOperation> ToOpenApi(this Tool tool)
    {
        return new Dictionary<string, OpenApiOperation>(StringComparer.Ordinal)
        {
            ["post"] = new()
            {
                Description = tool.Description,
                Summary = tool.Description,
                OperationId = tool.Name,
                RequestBody = new()
                {
                    Description = tool.Description,
                    Required = true,
                    Content = new(StringComparer.Ordinal)
                    {
                        ["application/json"] = new() { Schema = tool.InputSchema }
                    }
                },
                Responses = new(StringComparer.Ordinal)
                {
                    ["200"] = new()
                    {
                        Description = "Successful Response",
                        Content = new(StringComparer.Ordinal)
                        {
                            ["application/json"] = new() { Schema = unknownSchema }
                        }
                    },
                    ["422"] = new()
                    {
                        Description = "Validation Error",
                        Content = new(StringComparer.Ordinal)
                        {
                            ["application/json"] = new() { Schema = unknownSchema }
                        }
                    },
                    ["500"] = new()
                    {
                        Description = "Error Response",
                        Content = new(StringComparer.Ordinal)
                        {
                            ["application/json"] = new() { Schema = unknownSchema }
                        }
                    }
                }
            }
        };
    }
}