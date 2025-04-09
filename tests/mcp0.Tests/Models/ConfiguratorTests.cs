using System.Runtime.CompilerServices;

using ModelContextProtocol.Protocol.Transport;

namespace mcp0.Models;

[TestClass]
public sealed class ConfiguratorTests
{
    [TestMethod]
    public void ConfiguresStdioClientTransportCorrectly()
    {
        var server = new Server
        {
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"],
            WorkingDirectory = "/home/user",
            Environment = new() { { "KEY", "VALUE" } },
            ShutdownTimeout = 60
        };

        var transport = server.ToClientTransport("mcp0");

        Assert.IsInstanceOfType<StdioClientTransport>(transport);

        var actualOptions = GetTransportOptions((StdioClientTransport)transport);
        var expectedOptions = new StdioClientTransportOptions
        {
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"],
            WorkingDirectory = "/home/user",
            EnvironmentVariables = new() { { "KEY", "VALUE" } },
            ShutdownTimeout = TimeSpan.FromSeconds(60)
        };

        AreEqual(expectedOptions, actualOptions);
    }

    [TestMethod]
    public void ConfiguresSseClientTransportCorrectly()
    {
        var server = new Server
        {
            Url = new Uri("http://localhost:8080/server-everything"),
            Headers = new() { { "Authorization", "TOKEN" } },
            ConnectionTimeout = 30,
            MaxReconnectAttempts = 10,
            ReconnectDelay = 60
        };

        var transport = server.ToClientTransport("mcp0");

        Assert.IsInstanceOfType<SseClientTransport>(transport);

        var actualOptions = GetTransportOptions((SseClientTransport)transport);
        var expectedOptions = new SseClientTransportOptions
        {
            Endpoint = server.Url,
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
        var server = new Server { Command = "" };

        Assert.ThrowsException<InvalidOperationException>(() => server.ToClientTransport("mcp0"));
    }

    [TestMethod]
    public void ThrowsOnMissingUrl()
    {
        var server = new Server { ConnectionTimeout = 60 };

        Assert.ThrowsException<InvalidOperationException>(() => server.ToClientTransport("mcp0"));
    }

    [TestMethod]
    public void ThrowsOnMixedUpConfig()
    {
        var server = new Server
        {
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"],
            Url = new Uri("http://localhost:8080/server-everything")
        };

        Assert.ThrowsException<InvalidOperationException>(() => server.ToClientTransport("mcp0"));
    }

    private static void AreEqual(StdioClientTransportOptions expected, StdioClientTransportOptions actual)
    {
        Assert.AreEqual(expected.Command, actual.Command);
        Assert.IsTrue((expected.Arguments ?? []).SequenceEqual(actual.Arguments ?? []));
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_options")]
    private static extern ref StdioClientTransportOptions GetTransportOptions(StdioClientTransport transport);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_options")]
    private static extern ref SseClientTransportOptions GetTransportOptions(SseClientTransport transport);
}