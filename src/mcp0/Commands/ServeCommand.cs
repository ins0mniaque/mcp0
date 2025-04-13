using System.CommandLine;

using mcp0.Mcp;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class ServeCommand : ProxyCommand
{
    public ServeCommand() : base("serve", "Serve one or more configured contexts as an MCP server over HTTP with SSE")
    {
        var noReloadOption = new Option<bool>("--no-reload", "Do not reload when context configuration files change");
        var pathsArgument = new Argument<string[]>("files", "A list of context configuration files to serve")
        {
            Arity = ArgumentArity.OneOrMore
        };

        AddOption(noReloadOption);
        AddArgument(pathsArgument);

        this.SetHandler(Execute, pathsArgument, noReloadOption);
    }

    private Task Execute(string[] paths, bool noReload) => Execute(paths, noReload, CancellationToken.None);

    private async Task Execute(string[] paths, bool noReload, CancellationToken cancellationToken)
    {
        await ConnectAndRun(paths, noReload, LogLevel.Information, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(3001);
        });

        builder.Services.AddMcpServer(_ => proxy.GetServerOptions());

        var app = builder.Build();

        app.MapMcp();

        await app.RunAsync();
    }
}