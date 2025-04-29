using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using mcp0.Mcp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Types;

namespace mcp0.OpenApi;

internal static class McpOpenApiExtensions
{
    public static IEndpointConventionBuilder MapMcpOpenApi(this IEndpointRouteBuilder endpoints,
        McpProxy proxy, [StringSyntax("Route")] string pattern = "openapi.json", [StringSyntax("Route")] string toolPattern = "tools/{tool}")
    {
        return endpoints.MapGet(pattern, async httpContext =>
        {
            var document = proxy.Tools.ToOpenApiDocument(toolPattern);
            await httpContext.Response.WriteAsJsonAsync(document, OpenApiContext.Default.OpenApiDocument, null,
                httpContext.RequestAborted);
        });
    }

    public static IEndpointConventionBuilder MapMcpOpenApiTools(this IEndpointRouteBuilder endpoints,
        McpProxy proxy, [StringSyntax("Route")] string toolPattern = "tools/{tool}")
    {
        return endpoints.MapPost(toolPattern, async httpContext =>
        {
            if (httpContext.Request.RouteValues["tool"]?.ToString() is not { } toolName)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var tool = proxy.Tools.Find(toolName, out var client);
            var arguments = await JsonSerializer.DeserializeAsync(httpContext.Request.Body,
                OpenApiContext.Default.DictionaryStringObject, httpContext.RequestAborted);
            var response = await client.CallToolAsync(proxy.Tools.Unmap(tool), arguments, cancellationToken: httpContext.RequestAborted);
            if (response.IsError)
                httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            if (response.Content.Count is 1)
                await httpContext.Response.WriteContentAsync(response.Content[0], httpContext.RequestAborted);
            else if (response.Content.Count > 1)
                await httpContext.Response.WriteAsJsonAsync(response.Content, OpenApiContext.Default.ListContent, null,
                    httpContext.RequestAborted);
        });
    }

    private static async Task WriteContentAsync(this HttpResponse httpResponse, Content content, CancellationToken cancellationToken)
    {
        if (content.Data is { } data)
        {
            httpResponse.ContentType = content.MimeType ?? "application/octet-stream";

            await httpResponse.Body.WriteAsync(Convert.FromBase64String(data), cancellationToken);
        }
        else if (content.Text is { } text)
        {
            httpResponse.ContentType = content.MimeType ?? "text/plain";

            await httpResponse.WriteAsync(text, cancellationToken);
        }
        else if (content.Resource is BlobResourceContents blobResource)
        {
            httpResponse.ContentType ??= blobResource.MimeType ?? "application/octet-stream";

            await httpResponse.Body.WriteAsync(Convert.FromBase64String(blobResource.Blob), cancellationToken);
        }
        else if (content.Resource is TextResourceContents textResource)
        {
            httpResponse.ContentType ??= textResource.MimeType ?? "text/plain";

            await httpResponse.WriteAsync(textResource.Text, cancellationToken);
        }
    }
}