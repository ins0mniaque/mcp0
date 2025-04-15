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
                ["prompt"] = "This is a prompt.",
                ["argument"] = "This is a prompt with an {{argument}}.",
                ["optional"] = "This is a prompt with an optional {{argument?}}.",
                ["described"] = "This is a prompt with a described optional {{argument?#Argument description}}."
            },
            Resources = new(StringComparer.Ordinal)
            {
                ["file"] = "~/file.txt",
                ["image"] = "/path/to/image.png # Description for resource",
                ["random-cat"] = "https://cataas.com/cat # Downloads a random cat picture",
                ["data-uri"] = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg=="
            },
            Tools = new(StringComparer.Ordinal)
            {
                ["ping"] = "ping -c 1 {{host}}",
                ["add"] = "bc -e {{a:integer}}+{{b:integer}}+0{{c?:integer}} # Add two or three numbers using bc",
                ["git-status"] = "git -C {{repo_path#Path to the repository}} # Gets the status of a git repository",
            },
            Servers = new(StringComparer.Ordinal)
            {
                ["everything"] = new StdioServer
                {
                    Command = "npx",
                    Arguments = ["-y", "@modelcontextprotocol/server-everything"]
                },
                ["stdio"] = new StdioServer
                {
                    Command = "command", Arguments = ["arg1", "arg2", "arg3"],
                    WorkingDirectory = "~/workdir",
                    Environment = new(StringComparer.Ordinal) { ["NAME"] = "VALUE" },
                    EnvironmentFile = "~/secrets.env",
                    ShutdownTimeout = TimeSpan.FromSeconds(5)
                },
                ["sse"] = new SseServer { Url = new Uri("http://localhost:3001/sse") },
                ["sse-api-key"] = new SseServer
                {
                    Url = new Uri("https://mcp.example.com/sse"),
                    Headers = new(StringComparer.Ordinal) { ["X-API-Key"] = "SECRETAPIKEY" }
                },
                ["sse-token"] = new SseServer
                {
                    Url = new Uri("https://mcp.example.com/sse"),
                    Headers = new(StringComparer.Ordinal) { ["Authorization"] = "Bearer SECRETTOKEN" },
                    ConnectionTimeout = TimeSpan.FromSeconds(30),
                    MaxReconnectAttempts = 3,
                    ReconnectDelay = TimeSpan.FromSeconds(5)
                }
            }
        };
    }
}