using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;

namespace mcp0.Mcp;

[McpServerPromptType]
public static class Prompts
{
    [McpServerPrompt]
    public static ChatMessage Summarize(string content) => new(ChatRole.User, $"Please summarize: {content}");
}

[McpServerToolType]
public static class Tools
{
    [McpServerTool]
    public static string Echo(string message) => $"Echo: {message}";
}

[TestClass]
public sealed class McpProxyTests
{
    [TestMethod]
    public async Task ProxiesCorrectly()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = cancellationTokenSource.Token;

        await using var transport = new ClientServerTransport();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddMcpServer()
                        .WithStdioServerTransport()
                        .WithPromptsFromAssembly()
                        .WithToolsFromAssembly();

        builder.Services.RemoveAll<ITransport>()
                        .AddSingleton(transport.ServerTransport);

        var serverTask = builder.Build().RunAsync(cancellationToken);
        var client = await McpClientFactory.CreateAsync(transport.ClientTransport, cancellationToken: cancellationToken);

        await using var proxy = new McpProxy();

        await proxy.ConnectAsync([client], cancellationToken);

        await using var proxyTransport = new ClientServerTransport();
        await using var proxyServer = McpServerFactory.Create(proxyTransport.ServerTransport, proxy.GetServerOptions());

        var proxyServerTask = proxyServer.RunAsync(cancellationToken);
        var proxyClient = await McpClientFactory.CreateAsync(proxyTransport.ClientTransport, cancellationToken: cancellationToken);

        var prompts = await proxyClient.ListPromptsAsync(cancellationToken: cancellationToken);
        var tools = await proxyClient.ListToolsAsync(cancellationToken: cancellationToken);

        Assert.AreEqual("Summarize", prompts[0].ProtocolPrompt.Name);
        Assert.AreEqual("Echo", tools[0].ProtocolTool.Name);

        var promptArguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["content"] = "content" };
        var promptResult = await proxyClient.GetPromptAsync("Summarize", promptArguments, cancellationToken: cancellationToken);

        Assert.AreEqual("Please summarize: content", promptResult.Messages[0].Content.Text);

        var toolArguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["message"] = "message" };
        var toolResponse = await proxyClient.CallToolAsync("Echo", toolArguments, cancellationToken: cancellationToken);

        Assert.AreEqual("Echo: message", toolResponse.Content[0].Text);

        Assert.IsNotNull(proxy.Server);

        await cancellationTokenSource.CancelAsync();

        await proxyServerTask;
        await serverTask;
    }
}