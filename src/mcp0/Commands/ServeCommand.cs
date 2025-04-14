using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

using mcp0.Mcp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class ServeCommand : ProxyCommand
{
    public ServeCommand() : base("serve", "Serve one or more configured contexts as an MCP server over HTTP with SSE")
    {
        AddOption(HostOption);
        AddOption(OriginsOption);
        AddOption(ApiKeyOption);
        AddOption(SslCertFileOption);
        AddOption(SslKeyFileOption);
        AddOption(NoReloadOption);
        AddArgument(PathsArgument);
    }

    private Option<string> HostOption { get; } = new("--host", () => "http://localhost:7890", "IP or host addresses and ports to listen to");
    private Option<string> OriginsOption { get; } = new("--origins", "Allowed cross-origin requests origins");
    private Option<string> ApiKeyOption { get; } = new("--api-key", "API key to use for authentication");
    private Option<string> SslCertFileOption { get; } = new("--ssl-cert-file", "Path to PEM-encoded SSL certificate");
    private Option<string> SslKeyFileOption { get; } = new("--ssl-key-file", "Path to PEM-encoded SSL private key");

    private Argument<string[]> PathsArgument { get; } = new("files", "A list of context configuration files to serve")
    {
        Arity = ArgumentArity.OneOrMore
    };

    protected override async Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        var paths = PathsArgument.GetValue(context);

        await ConnectAndRun(context, paths, LogLevel.Information, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        var host = HostOption.GetRequiredValue("MCP0_HOST", context);
        var origins = OriginsOption.GetValue("MCP0_ORIGINS", context);
        var apiKey = ApiKeyOption.GetValue("MCP0_API_KEY", context);
        var sslCertFile = SslCertFileOption.GetValue("MCP0_SSL_CERT_FILE", context);
        var sslKeyFile = SslKeyFileOption.GetValue("MCP0_SSL_KEY_FILE", context);

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls(host);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;

            if (sslCertFile is not null && sslKeyFile is not null)
                options.ConfigureHttpsDefaults(httpsOptions =>
                    httpsOptions.ServerCertificate = LoadCertificate(sslCertFile, sslKeyFile));
        });

        if (proxy.Services?.GetService<ILoggerFactory>() is { } loggerFactory)
            builder.Services.AddSingleton(loggerFactory);

        builder.Services.AddMcpServer(proxy.ConfigureServerOptions);

        var app = builder.Build();

        if (origins is not null)
            app.UseCors(policy => policy.WithOrigins(origins.Split(',')));

        var endpoints = app.MapMcp();
        if (apiKey is not null)
            endpoints.AddEndpointFilter(new AuthorizationEndpointFilter(apiKey));

        await app.RunAsync();
    }

    private static X509Certificate2 LoadCertificate(string sslCertFile, string sslKeyFile)
    {
        var certificate = X509Certificate2.CreateFromPemFile(sslCertFile, sslKeyFile);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            certificate = X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Pkcs12));

        return certificate;
    }

    private sealed class AuthorizationEndpointFilter(string apiKeyOrToken) : IEndpointFilter
    {
        private string ApiKey { get; } = apiKeyOrToken;
        private string Authorization { get; } = $"Bearer {apiKeyOrToken}";

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            if (context.HttpContext.Request.Headers.TryGetValue("Authorization", out var authorization))
            {
                if (!string.Equals(authorization, Authorization, StringComparison.Ordinal))
                    return Results.Unauthorized();
            }
            else if (context.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var apiKey))
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