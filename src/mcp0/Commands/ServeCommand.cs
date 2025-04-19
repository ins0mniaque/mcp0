using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Cryptography.X509Certificates;

using mcp0.Mcp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace mcp0.Commands;

internal sealed class ServeCommand : ProxyCommand
{
    public ServeCommand() : base("serve", "Serve an MCP server over SSE built from one or more configuration files")
    {
        AddOption(HostOption);
        AddOption(OriginsOption);
        AddOption(ApiKeyOption);
        AddOption(SslCertFileOption);
        AddOption(SslKeyFileOption);
    }

    private static Option<string> HostOption { get; } = new("--host", () => "http://localhost:7890", "IP/host addresses and ports to listen to\n[env: MCP0_HOST]");
    private static Option<string> OriginsOption { get; } = new("--origins", "Allowed origins for cross-origin requests\n[env: MCP0_ORIGINS] [default: <host>]");
    private static Option<string> ApiKeyOption { get; } = new("--api-key", "API key to use for authentication\n[env: MCP0_API_KEY]");
    private static Option<string> SslCertFileOption { get; } = new("--ssl-cert-file", "Path to PEM-encoded SSL certificate\n[env: MCP0_SSL_CERT_FILE]");
    private static Option<string> SslKeyFileOption { get; } = new("--ssl-key-file", "Path to PEM-encoded SSL private key\n[env: MCP0_SSL_KEY_FILE]");

    protected override Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        return ConnectAndRun(context, LogLevel.Information, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        var host = HostOption.GetRequiredValue("MCP0_HOST", context);
        var origins = OriginsOption.GetValue("MCP0_ORIGINS", context) ?? host;
        var apiKey = ApiKeyOption.GetValue("MCP0_API_KEY", context);
        var sslCertFile = SslCertFileOption.GetValue("MCP0_SSL_CERT_FILE", context);
        var sslKeyFile = SslKeyFileOption.GetValue("MCP0_SSL_KEY_FILE", context);

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls(host.Split(',', ';'));

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;

            if (sslCertFile is not null && sslKeyFile is not null)
                options.ConfigureHttpsDefaults(httpsOptions =>
                    httpsOptions.ServerCertificate = LoadCertificate(sslCertFile, sslKeyFile));
        });

        var serviceProvider = context.GetServiceProvider();
        if (serviceProvider.GetService<ILoggerFactory>() is { } loggerFactory)
            builder.Services.AddSingleton(loggerFactory);

        builder.Services.AddCors();
        builder.Services.AddMcpServer(proxy.ConfigureServerOptions).WithHttpTransport();

        var app = builder.Build();

        app.Use(SecurityHeadersMiddleware);

        app.UseCors(policy =>
            policy.WithOrigins(origins.Split(',', ';'))
                  .WithMethods(HttpMethods.Get, HttpMethods.Post)
                  .WithHeaders(HeaderNames.Accept, HeaderNames.ContentType, HeaderNames.Origin,
                               HeaderNames.Authorization, AuthorizationEndpointFilter.ApiKeyHeaderName));

        var endpoints = app.MapMcp();
        if (apiKey is not null)
            endpoints.AddEndpointFilter(new AuthorizationEndpointFilter(apiKey));

        await app.RunAsync();
    }

    private static X509Certificate2 LoadCertificate(string sslCertFile, string sslKeyFile)
    {
        var certificate = X509Certificate2.CreateFromPemFile(sslCertFile, sslKeyFile);
        if (OperatingSystem.IsWindows())
            certificate = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Pkcs12));

        return certificate;
    }

    private static Task SecurityHeadersMiddleware(HttpContext context, Func<Task> next)
    {
        var headers = context.Response.Headers;

        if (context.Request.IsHttps)
            headers[HeaderNames.StrictTransportSecurity] = "max-age=31536000";

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "Deny";
        headers[HeaderNames.ContentSecurityPolicy] = "default-src: none; frame-ancestors 'none'";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), fullscreen=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), midi=(), payment=(), picture-in-picture=(), publickey-credentials-get=(), screen-wake-lock=(), sync-xhr=(), usb=(), web-share=(), xr-spatial-tracking=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Embedder-Policy"] = "require-corp";
        headers["Cross-Origin-Resource-Policy"] = "same-site";

        return next();
    }

    private sealed class AuthorizationEndpointFilter(string apiKeyOrToken) : IEndpointFilter
    {
        public const string ApiKeyHeaderName = "X-API-Key";

        private string ApiKey { get; } = apiKeyOrToken;
        private string Authorization { get; } = $"Bearer {apiKeyOrToken}";

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            if (context.HttpContext.Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorization))
            {
                if (!string.Equals(authorization, Authorization, StringComparison.Ordinal))
                    return Results.Unauthorized();
            }
            else if (context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey))
            {
                if (!string.Equals(apiKey, ApiKey, StringComparison.Ordinal))
                    return Results.Unauthorized();
            }
            else
                return Results.BadRequest();

            return await next(context);
        }
    }
}