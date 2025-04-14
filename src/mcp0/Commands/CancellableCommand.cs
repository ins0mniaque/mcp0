using System.CommandLine;
using System.CommandLine.Invocation;

namespace mcp0.Commands;

internal abstract class CancellableCommand : Command
{
    protected CancellableCommand(string name, string? description) : base(name, description)
    {
        this.SetHandler(Execute);
    }

    protected abstract Task Execute(InvocationContext context, CancellationToken cancellationToken);

    private Task Execute(InvocationContext context) => Execute(context, context.GetCancellationToken());
}