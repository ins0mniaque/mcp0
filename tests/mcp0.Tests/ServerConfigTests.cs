using System.Reflection;
using System.Runtime.CompilerServices;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

namespace mcp0.Tests;

[TestClass]
public sealed class ServerConfigTests
{
    [TestMethod]
    public void ConfiguresStdioClientTransportCorrectly()
    {
        var config = new ServerConfig
        {
            Command = "uvx",
            Arguments = ["mcp-server-fetch"],
            WorkingDirectory = "/home/user",
            Environment = new() { { "KEY", "VALUE" } },
            ShutdownTimeout = 60
        };

        var mcpServerConfig = config.ToMcpServerConfig("mcp0");
        var transport = CreateTransport(mcpServerConfig);

        Assert.IsInstanceOfType<StdioClientTransport>(transport);

        Assert.AreEqual(config.Command, mcpServerConfig.Location);

        var actualOptions = GetTransportOptions((StdioClientTransport)transport);
        var expectedOptions = new StdioClientTransportOptions
        {
            Command = "uvx",
            Arguments = "mcp-server-fetch",
            WorkingDirectory = "/home/user",
            EnvironmentVariables = new() { { "KEY", "VALUE" } },
            ShutdownTimeout = TimeSpan.FromSeconds(60)
        };

        AreEqual(expectedOptions, actualOptions);
    }

    [TestMethod]
    public void ConfiguresSseClientTransportCorrectly()
    {
        var config = new ServerConfig
        {
            Url = new Uri("http://localhost:8080/mcp-server-fetch"),
            Headers = new() { { "Authorization", "TOKEN" } },
            ConnectionTimeout = 30,
            MaxReconnectAttempts = 10,
            ReconnectDelay = 60
        };

        var mcpServerConfig = config.ToMcpServerConfig("mcp0");
        var transport = CreateTransport(mcpServerConfig);

        Assert.IsInstanceOfType<SseClientTransport>(transport);

        Assert.AreEqual(config.Url.ToString(), mcpServerConfig.Location);

        var actualOptions = GetTransportOptions((SseClientTransport)transport);
        var expectedOptions = new SseClientTransportOptions
        {
            AdditionalHeaders = new() { { "Authorization", "TOKEN" } },
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            MaxReconnectAttempts = 10,
            ReconnectDelay = TimeSpan.FromSeconds(60)
        };

        AreEqual(expectedOptions, actualOptions);
    }

    [TestMethod]
    public void ThrowsOnMissingCommand()
    {
        var config = new ServerConfig { Command = "" };

        Assert.ThrowsException<InvalidOperationException>(() => config.ToMcpServerConfig("mcp0"));
    }

    [TestMethod]
    public void ThrowsOnMissingUrl()
    {
        var config = new ServerConfig { ConnectionTimeout = 60 };

        Assert.ThrowsException<InvalidOperationException>(() => config.ToMcpServerConfig("mcp0"));
    }

    [TestMethod]
    public void ThrowsOnMixedUpConfig()
    {
        var config = new ServerConfig
        {
            Command = "uvx",
            Url = new Uri("http://localhost:8080/mcp-server-fetch")
        };

        Assert.ThrowsException<InvalidOperationException>(() => config.ToMcpServerConfig("mcp0"));
    }

    private static void AreEqual(StdioClientTransportOptions expected, StdioClientTransportOptions actual)
    {
        Assert.AreEqual(expected.Command, actual.Command);
        Assert.AreEqual(expected.Arguments, actual.Arguments);
        Assert.AreEqual(expected.WorkingDirectory, actual.WorkingDirectory);
        Assert.AreEqual(expected.ShutdownTimeout, actual.ShutdownTimeout);

        if (expected.EnvironmentVariables is not null)
        {
            Assert.IsNotNull(actual.EnvironmentVariables);
            foreach (var variable in expected.EnvironmentVariables)
            {
                Assert.IsTrue(actual.EnvironmentVariables.ContainsKey(variable.Key));
                Assert.AreEqual(actual.EnvironmentVariables[variable.Key], variable.Value);
            }
        }
    }

    private static void AreEqual(SseClientTransportOptions expected, SseClientTransportOptions actual)
    {
        Assert.AreEqual(expected.ConnectionTimeout, actual.ConnectionTimeout);
        Assert.AreEqual(expected.MaxReconnectAttempts, actual.MaxReconnectAttempts);
        Assert.AreEqual(expected.MaxReconnectAttempts, actual.MaxReconnectAttempts);

        if (expected.AdditionalHeaders is not null)
        {
            Assert.IsNotNull(actual.AdditionalHeaders);
            foreach (var header in expected.AdditionalHeaders)
            {
                Assert.IsTrue(actual.AdditionalHeaders.ContainsKey(header.Key));
                Assert.AreEqual(actual.AdditionalHeaders[header.Key], header.Value);
            }
        }
    }

    private static IClientTransport CreateTransport(McpServerConfig config)
    {
        var createTransportMethod = typeof(McpClientFactory).GetMethod("CreateTransport", BindingFlags.NonPublic | BindingFlags.Static);
        if (createTransportMethod is null)
            throw new InvalidOperationException("ModelContextProtocol infrastructure changed: Unable to find the McpClientFactory.CreateTransport method");

        if (createTransportMethod.Invoke(null, [config, null]) is not IClientTransport transport)
            throw new InvalidOperationException("ModelContextProtocol infrastructure changed: McpClientFactory.CreateTransport method did not return an instance of IClientTransport");

        return transport;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_options")]
    private extern static ref StdioClientTransportOptions GetTransportOptions(StdioClientTransport transport);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_options")]
    private extern static ref SseClientTransportOptions GetTransportOptions(SseClientTransport transport);
}
