using System.CommandLine.Invocation;

using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class RunCommand : ProxyCommand
{
    public RunCommand() : base("run", "Run an MCP server over STDIO built from one or more configuration files")
    {
        AddOption(NoReloadOption);
        AddArgument(PathsArgument);
    }

    protected override async Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        var paths = PathsArgument.GetValue(context);

        await ConnectAndRun(context, paths, LogLevel.Information, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, InvocationContext context, CancellationToken cancellationToken)
    {
        await proxy.RunAsync(cancellationToken);
    }
}