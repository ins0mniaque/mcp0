using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;

using mcp0.Core;
using mcp0.Models;

namespace mcp0.Commands;

internal sealed class NewCommand : CancellableCommand
{
    public NewCommand() : base("new", "Create a new configuration file")
    {
        AddArgument(PathArgument);
    }

    private static Argument<string?> PathArgument { get; } = new("file", "The configuration output file (stdout if unspecified)")
    {
        Arity = ArgumentArity.ZeroOrOne
    };

    protected override async Task Execute(InvocationContext context, CancellationToken cancellationToken)
    {
        var path = PathArgument.GetValue(context);
        if (path is not null && !Path.HasExtension(path))
            path = Path.ChangeExtension(path, ".json");

        var configuration = new Configuration
        {
            Servers = new()
            {
                { "everything", new StdioServer { Command = "npx", Arguments = ["-y", "@modelcontextprotocol/server-everything"] } }
            }
        };

        await using var stream = path is null ? Terminal.OpenStdOut() : File.Create(Posix.ExpandPath(path));
        await JsonSerializer.SerializeAsync(stream, configuration, ModelContext.Default.Configuration, cancellationToken);

        Terminal.WriteLine(path is null ? string.Empty : $"{path} created");
    }
}