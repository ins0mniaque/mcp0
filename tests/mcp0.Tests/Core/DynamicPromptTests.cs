using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace mcp0.Core;

[TestClass]
public sealed class DynamicPromptTests
{
    [TestMethod]
    public void ParsesArgumentCorrectly()
    {
        var prompt = new Models.Prompt { Messages = [new() { Template = "{{argument}}" }] };
        var arguments = new DynamicPrompt(string.Empty, prompt).Prompt.Arguments;
        var expected = new PromptArgument { Name = "argument", Description = null, Required = true };

        Assert.IsNotNull(arguments);
        Assert.AreEqual(1, arguments.Count);
        AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesRequiredCorrectly()
    {
        var prompt = new Models.Prompt { Messages = [new() { Template = "{{argument?}}" }] };
        var arguments = new DynamicPrompt(string.Empty, prompt).Prompt.Arguments;
        var expected = new PromptArgument { Name = "argument", Description = null };

        Assert.IsNotNull(arguments);
        Assert.AreEqual(1, arguments.Count);
        AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesDescriptionCorrectly()
    {
        var prompt = new Models.Prompt { Messages = [new() { Template = "{{argument#desc}}" }] };
        var arguments = new DynamicPrompt(string.Empty, prompt).Prompt.Arguments;
        var expected = new PromptArgument { Name = "argument", Description = "desc", Required = true };

        Assert.IsNotNull(arguments);
        Assert.AreEqual(1, arguments.Count);
        AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesTemplateCorrectly()
    {
        var template =
        """
        These are arguments: {{argument}} {{_underscore}} {{numbered0}} {{optional?}} {{described?#desc}}.
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        var prompt = new Models.Prompt { Messages = [new() { Template = template }] };
        var arguments = new DynamicPrompt(string.Empty, prompt).Prompt.Arguments;
        var expected = new PromptArgument[]
        {
            new() { Name = "argument", Description = null, Required = true },
            new() { Name = "_underscore", Description = null, Required = true },
            new() { Name = "numbered0", Description = null, Required = true },
            new() { Name = "optional", Description = null },
            new() { Name = "described", Description = "desc" }
        };

        Assert.IsNotNull(arguments);
        Assert.AreEqual(5, arguments.Count);
        for (var index = 0; index < expected.Length; index++)
            AreEqual(expected[index], arguments[index]);
    }

    [TestMethod]
    public async Task RendersTemplateCorrectly()
    {
        var template =
        """
        These are arguments: {{argument}} {{_underscore}} {{numbered0}} {{optional?}} {{described?#desc}}.
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        await using var server = new McpServer();

        var prompt = new Models.Prompt { Messages = [new() { Template = template }] };
        var actual = await new DynamicPromptTemplate(prompt).Render(server, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["argument"] = "value",
            ["optional"] = "option"
        }, CancellationToken.None);

        var expected =
        """
        These are arguments: value {{_underscore}} {{numbered0}} option .
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        Assert.AreEqual(1, actual.Count);
        Assert.AreEqual(Role.User, actual[0].Role);
        Assert.AreEqual(expected, actual[0].Content.Text);
    }

    [TestMethod]
    public async Task RendersDynamicTemplateCorrectly()
    {
        await using var server = new McpServer();

        var document = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
        var prompt = new Models.Prompt
        {
            Messages =
            [
                new() { Template = "Summarize this document: {{document}}", ReturnArgument = "summary" },
                new() { Template = "Given this document: {{document}} and summary: {{summary}}, provide feedback on the summary.", ReturnArgument = "feedback" },
                new() { Template = "Given this document: {{document}}, summary: {{summary}} and feedback: {{feedback}}, update on the summary based on the feedback.", ReturnArgument = string.Empty }
            ]
        };

        var actual = await new DynamicPromptTemplate(prompt).Render(server, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["document"] = document
        }, CancellationToken.None);

        Assert.AreEqual(6, actual.Count);
        Assert.AreEqual(Role.User, actual[0].Role);
        Assert.AreEqual(Role.Assistant, actual[1].Role);
        Assert.AreEqual(Role.User, actual[2].Role);
        Assert.AreEqual(Role.Assistant, actual[3].Role);
        Assert.AreEqual(Role.User, actual[4].Role);
        Assert.AreEqual(Role.Assistant, actual[5].Role);
        Assert.AreEqual($"Summarize this document: {document}", actual[0].Content.Text);
        Assert.AreEqual(McpServer.Response, actual[1].Content.Text);
        Assert.AreEqual($"Given this document: {document} and summary: {McpServer.Response}, provide feedback on the summary.", actual[2].Content.Text);
        Assert.AreEqual(McpServer.Response, actual[3].Content.Text);
        Assert.AreEqual($"Given this document: {document}, summary: {McpServer.Response} and feedback: {McpServer.Response}, update on the summary based on the feedback.", actual[4].Content.Text);
        Assert.AreEqual(McpServer.Response, actual[5].Content.Text);
    }

    private static void AreEqual(PromptArgument expected, PromptArgument actual)
    {
        Assert.AreEqual(expected.Name, actual.Name);
        Assert.AreEqual(expected.Description, actual.Description);
        Assert.AreEqual(expected.Required, actual.Required);
    }

    [SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty", Justification = "Unused IMcpServer property")]
    private sealed class McpServer : IMcpServer
    {
        public const string Response = "MODEL RESPONSE";

        public ClientCapabilities? ClientCapabilities { get; } = new() { Sampling = new() };
        public Implementation? ClientInfo { get; }
        public McpServerOptions ServerOptions { get; } = new();
        public IServiceProvider? Services { get; }
        public LoggingLevel? LoggingLevel { get; }

        public async Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Method is not RequestMethods.SamplingCreateMessage)
                throw new NotImplementedException();

            return await Task.FromResult(new JsonRpcResponse
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToNode(new CreateMessageResult
                {
                    Model = "model",
                    Role = Role.Assistant,
                    Content = new() { Text = Response }
                }),
            });
        }

        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncDisposable RegisterNotificationHandler(string method, Func<JsonRpcNotification, CancellationToken, ValueTask> handler) => throw new NotImplementedException();
        public Task RunAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}