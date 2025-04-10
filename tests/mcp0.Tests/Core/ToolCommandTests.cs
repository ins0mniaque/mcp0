namespace mcp0.Core;

[TestClass]
public sealed class ToolCommandTests
{
    [TestMethod]
    public void ParsesCommandsCorrectly()
    {
        var env = ToolCommand.Parse("bc -e {{expression}}", new Dictionary<string, string>
        {
            { "expression", "2 + 2" }
        });

        Assert.AreEqual(3, env.Length);
        Assert.AreEqual("bc", env[0]);
        Assert.AreEqual("-e", env[1]);
        Assert.AreEqual("2 + 2", env[2]);
    }

    [TestMethod]
    public void ParsesCommandsWithQuotesCorrectly()
    {
        var env = ToolCommand.Parse("ollama run {{model}} \"Help me with this task: {{task}}\"", new Dictionary<string, string>
        {
            { "model", "deepseek-r1" },
            { "task", "Write a story about a cat" }
        });

        Assert.AreEqual(4, env.Length);
        Assert.AreEqual("ollama", env[0]);
        Assert.AreEqual("run", env[1]);
        Assert.AreEqual("deepseek-r1", env[2]);
        Assert.AreEqual("Help me with this task: Write a story about a cat", env[3]);
    }
}