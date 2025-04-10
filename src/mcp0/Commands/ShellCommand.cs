using System.CommandLine;

namespace mcp0.Commands;

internal sealed class ShellCommand : Command
{
    public ShellCommand() : base("shell", "Run an interactive shell on the MCP server for one or more configured contexts")
    {
        var pathsArgument = new Argument<string[]>("files", "A list of context configuration files to run at start")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        AddAlias("sh");
        AddArgument(pathsArgument);

        this.SetHandler(Execute, pathsArgument);
    }

    private static Task Execute(string[] paths) => Execute(paths, CancellationToken.None);

    private static async Task Execute(string[] paths, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Terminal.Write("> ");

            var input = Terminal.ReadLine()?.Trim();
            if (input == "exit")
                break;

            Terminal.WriteLine($"Input: {input}");

            await Task.CompletedTask;
        }
    }
}