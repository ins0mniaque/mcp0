using System.CommandLine;
using System.CommandLine.Invocation;

using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class RunCommand : ProxyCommand
{
    public RunCommand() : base("run", "Run one or more configured contexts as an MCP server")
    {
        AddOption(NoReloadOption);
        AddArgument(PathsArgument);
    }

    private Argument<string[]> PathsArgument { get; } = new("files", "A list of context configuration files to run")
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
        await proxy.RunAsync(cancellationToken);
    }
}