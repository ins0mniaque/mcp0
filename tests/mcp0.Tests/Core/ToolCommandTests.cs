namespace mcp0.Core;

[TestClass]
public sealed class ToolCommandTests
{
    [TestMethod]
    public void ParsesCommandsCorrectly()
    {
        var commandLine = ToolCommand.Parse("bc -e {{expression}}", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["expression"] = "2 + 2"
        });

        Assert.AreEqual(3, commandLine.Length);
        Assert.AreEqual("bc", commandLine[0]);
        Assert.AreEqual("-e", commandLine[1]);
        Assert.AreEqual("2 + 2", commandLine[2]);
    }

    [TestMethod]
    public void ParsesCommandsWithQuotesCorrectly()
    {
        var commandLine = ToolCommand.Parse("ollama run {{model}} \"Help me with this task: {{task}}\"", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model"] = "deepseek-r1",
            ["task"] = "Write a story about a cat"
        });

        Assert.AreEqual(4, commandLine.Length);
        Assert.AreEqual("ollama", commandLine[0]);
        Assert.AreEqual("run", commandLine[1]);
        Assert.AreEqual("deepseek-r1", commandLine[2]);
        Assert.AreEqual("Help me with this task: Write a story about a cat", commandLine[3]);
    }

    [TestMethod]
    public void ParsesCommandsWithEnvironmentCorrectly()
    {
        var commandLine = ToolCommand.Parse("BC_ENV_ARGS=~/.bcrc bc -e {{expression}}", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["expression"] = "2 + 2"
        });

        Assert.AreEqual(4, commandLine.Length);
        Assert.AreEqual("BC_ENV_ARGS=~/.bcrc", commandLine[0]);
        Assert.AreEqual("bc", commandLine[1]);
        Assert.AreEqual("-e", commandLine[2]);
        Assert.AreEqual("2 + 2", commandLine[3]);

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        var commandIndex = CommandLine.ParseEnvironment(commandLine, environment);

        Assert.AreEqual(1, commandIndex);
        Assert.AreEqual(1, environment.Count);
        Assert.AreEqual("~/.bcrc", environment["BC_ENV_ARGS"]);
    }

    [TestMethod]
    public void ParsesCommandsWithEnvironmentWithQuotesCorrectly()
    {
        var commandLine = ToolCommand.Parse("BC_ENV_ARGS=\"~/s p a c e/bcrc\" bc -e {{expression}}", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["expression"] = "2 + 2"
        });

        Assert.AreEqual(4, commandLine.Length);
        Assert.AreEqual("BC_ENV_ARGS=~/s p a c e/bcrc", commandLine[0]);
        Assert.AreEqual("bc", commandLine[1]);
        Assert.AreEqual("-e", commandLine[2]);
        Assert.AreEqual("2 + 2", commandLine[3]);

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        var commandIndex = CommandLine.ParseEnvironment(commandLine, environment);

        Assert.AreEqual(1, commandIndex);
        Assert.AreEqual(1, environment.Count);
        Assert.AreEqual("~/s p a c e/bcrc", environment["BC_ENV_ARGS"]);
    }
}