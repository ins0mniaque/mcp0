using System.CommandLine;

using mcp0.Mcp;

using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class RunCommand : ProxyCommand
{
    public RunCommand() : base("run", "Run one or more configured contexts as an MCP server")
    {
        var noReloadOption = new Option<bool>("--no-reload", "Do not reload when context configuration files change");
        var pathsArgument = new Argument<string[]>("files", "A list of context configuration files to run")
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
        await proxy.RunAsync(cancellationToken);
    }
}