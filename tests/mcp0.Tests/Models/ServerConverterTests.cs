using System.Text.Json;

namespace mcp0.Models;

[TestClass]
public sealed class ServerConverterTests
{
    [TestMethod]
    public void DeserializesStringToStdioServerCorrectly()
    {
        var json = "\"npx -y @modelcontextprotocol/server-everything\"";

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Server);
        var expected = new StdioServer
        {
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"]
        };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeserializesObjectToStdioServerCorrectly()
    {
        var json =
        """
        {
          "name": "server-everything",
          "command": "npx",
          "args": [
            "-y",
            "@modelcontextprotocol/server-everything"
          ],
          "workDir": "/home/user",
          "env": { "KEY": "VALUE" },
          "shutdownTimeout": 60
        }
        """;

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Server);
        var expected = new StdioServer
        {
            Name = "server-everything",
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"],
            WorkingDirectory = "/home/user",
            Environment = new(StringComparer.Ordinal) { ["KEY"] = "VALUE" },
            ShutdownTimeout = TimeSpan.FromSeconds(60)
        };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeserializesStringToSseServerCorrectly()
    {
        var json = "\"http://localhost:8080/server-everything\"";

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Server);
        var expected = new SseServer
        {
            Url = new Uri("http://localhost:8080/server-everything")
        };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeserializesObjectToSseServerCorrectly()
    {
        var json =
        """
        {
          "name": "server-everything",
          "url": "http://localhost:8080/server-everything",
          "headers": { "Authorization": "TOKEN" },
          "connectionTimeout": 30
        }
        """;

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Server);
        var expected = new SseServer
        {
            Name = "server-everything",
            Url = new Uri("http://localhost:8080/server-everything"),
            Headers = new(StringComparer.Ordinal) { ["Authorization"] = "TOKEN" },
            ConnectionTimeout = TimeSpan.FromSeconds(30)
        };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SerializesStdioServerToStringCorrectly()
    {
        var expected = new StdioServer
        {
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"]
        };

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Server);
        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Server);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SerializesStdioServerToObjectCorrectly()
    {
        var expected = new StdioServer
        {
            Name = "server-everything",
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"],
            WorkingDirectory = "/home/user",
            Environment = new(StringComparer.Ordinal) { ["KEY"] = "VALUE" },
            ShutdownTimeout = TimeSpan.FromSeconds(60)
        };

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Server);
        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Server);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SerializesSseServerToStringCorrectly()
    {
        var expected = new SseServer
        {
            Url = new Uri("http://localhost:8080/server-everything")
        };

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Server);
        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Server);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SerializesSseServerToObjectCorrectly()
    {
        var expected = new SseServer
        {
            Name = "server-everything",
            Url = new Uri("http://localhost:8080/server-everything"),
            Headers = new(StringComparer.Ordinal) { ["Authorization"] = "TOKEN" },
            ConnectionTimeout = TimeSpan.FromSeconds(30)
        };

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Server);
        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Server);

        Assert.AreEqual(expected, actual);
    }
}