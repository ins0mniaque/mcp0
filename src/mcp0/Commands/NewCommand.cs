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

        var configuration = GenerateConfiguration();

        await using var stream = path is null ? Terminal.OpenStdOut() : File.Create(Posix.ExpandPath(path));
        await JsonSerializer.SerializeAsync(stream, configuration, ModelContext.Default.Configuration, cancellationToken);

        Terminal.WriteLine(path is null ? string.Empty : $"{path} created");
    }

    private static Configuration GenerateConfiguration()
    {
        return new()
        {
            Prompts = new(StringComparer.Ordinal)
            {
                ["prompt"] = new() { Messages = [new() { Template = "This is a prompt." }] },
                ["argument"] = new() { Messages = [new() { Template = "This is a prompt with an {{argument}}." }] },
                ["optional"] = new() { Messages = [new() { Template = "This is a prompt with an optional {{argument?}}." }] },
                ["described"] = new() { Messages = [new() { Template = "This is a prompt with a described optional {{argument?#Argument description}}." }] }
            },
            Resources = new(StringComparer.Ordinal)
            {
                ["file"] = new() { Uri = new Uri(Posix.ExpandPath("~/file.txt"), UriKind.Absolute) },
                ["image"] = new() { Uri = new Uri("/path/to/image.png", UriKind.Absolute), Description = "Description for resource" },
                ["random-cat"] = new() { Uri = new Uri("https://cataas.com/cat", UriKind.Absolute), Description = "Downloads a random cat picture" },
                ["data-uri"] = new() { Uri = new Uri("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==", UriKind.Absolute) }
            },
            Tools = new(StringComparer.Ordinal)
            {
                ["ping"] = new() { Command = "ping -c 1 {{host}}" },
                ["add"] = new()
                {
                    Command = "bc -e {{a:integer}}+{{b:integer}}+0{{c?:integer}}",
                    Description = "Add two or three numbers using bc"
                },
                ["git-status"] = new()
                {
                    Command = "git -C {{repo_path#Path to the repository}}",
                    Description = "Gets the status of a git repository"
                },
            },
            Servers =
            [
                new StdioServer
                {
                    Name = "everything",
                    Command = "npx",
                    Arguments = ["-y", "@modelcontextprotocol/server-everything"]
                },
                new StdioServer
                {
                    Name = "stdio",
                    Command = "command",
                    Arguments = ["arg1", "arg2", "arg3"],
                    WorkingDirectory = "~/workdir",
                    Environment = new(StringComparer.Ordinal) { ["NAME"] = "VALUE" },
                    EnvironmentFile = "~/secrets.env",
                    ShutdownTimeout = TimeSpan.FromSeconds(5)
                },
                new SseServer { Name = "sse", Url = new Uri("http://localhost:3001/sse") },
                new SseServer
                {
                    Name = "sse-api-key",
                    Url = new Uri("https://mcp.example.com/sse"),
                    Headers = new(StringComparer.Ordinal) { ["X-API-Key"] = "SECRETAPIKEY" }
                },
                new SseServer
                {
                    Name = "sse-token",
                    Url = new Uri("https://mcp.example.com/sse"),
                    Headers = new(StringComparer.Ordinal) { ["Authorization"] = "Bearer SECRETTOKEN" },
                    ConnectionTimeout = TimeSpan.FromSeconds(30)
                }
            ]
        };
    }
}