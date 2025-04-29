using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace mcp0.OpenApi;

internal static class ScalarExtensions
{
    public static IEndpointConventionBuilder MapScalar(this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = "scalar", [StringSyntax("Route")] string openApiPattern = "openapi.json")
    {
        return endpoints.MapGet(pattern, async httpContext =>
        {
            httpContext.Response.ContentType = MediaTypeNames.Text.Html;

            var openApiEndpoint = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{openApiPattern}";

            await httpContext.Response.WriteAsync(Scalar(openApiEndpoint), httpContext.RequestAborted);
        });
    }

    private static string Scalar(string openApiEndpoint) =>
    $$"""
    <!doctype html>
    <html>
      <head>
        <title>Scalar API Reference</title>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </head>

      <body>
        <div id="app"></div>
        <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
        <script>
          Scalar.createApiReference('#app', {url: '{{openApiEndpoint}}'})
        </script>
      </body>
    </html>
    """;
}