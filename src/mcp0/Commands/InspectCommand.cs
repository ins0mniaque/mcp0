using System.CommandLine.Invocation;

using mcp0.Core;
using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class InspectCommand : ProxyCommand
{
    public InspectCommand() : base("inspect", "Inspect an MCP server built from one or more configuration files")
    {
        AddAlias("i");
    }

    protected override Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        return ConnectAndRun(context, LogLevel.Warning, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        Inspector.Inspect(proxy);

        await Task.CompletedTask;
    }
}