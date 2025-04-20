using System.CommandLine.Invocation;

using mcp0.Mcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class RunCommand : ProxyCommand
{
    public RunCommand() : base("run", "Run an MCP server over STDIO built from one or more configuration files") { }

    protected override Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        return ConnectAndRun(context, LogLevel.Information, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();

        var serviceProvider = context.GetServiceProvider();
        if (serviceProvider.GetService<ILoggerFactory>() is { } loggerFactory)
            builder.Services.AddSingleton(loggerFactory);

        builder.Services.AddMcpServer(proxy.ConfigureServerOptions)
                        .WithStdioServerTransport();

        await builder.Build().RunAsync(cancellationToken);
    }
}