using System.CommandLine;

using mcp0.Core;
using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class InspectCommand : ProxyCommand
{
    public InspectCommand() : base("inspect", "Inspect the MCP server for one or more configured contexts")
    {
        var pathsArgument = new Argument<string[]>("files", "A list of context configuration files to inspect")
        {
            Arity = ArgumentArity.OneOrMore
        };

        AddAlias("i");
        AddArgument(pathsArgument);

        this.SetHandler(Execute, pathsArgument);
    }

    private Task Execute(string[] paths) => Execute(paths, CancellationToken.None);

    private async Task Execute(string[] paths, CancellationToken cancellationToken)
    {
        await ConnectAndRun(paths, noReload: true, LogLevel.Warning, cancellationToken);
    }

    protected override async Task Run(McpProxy proxy, CancellationToken cancellationToken)
    {
        Inspector.Inspect(proxy);

        await Task.CompletedTask;
    }
}