namespace mcp0.Core;

[TestClass]
public sealed class CommandLineTemplateTests
{
    [TestMethod]
    public void RendersCommandsCorrectly()
    {
        var startInfo = new CommandLineTemplate("bc -e {{expression}}").Render(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["expression"] = "2 + 2"
        });

        Assert.AreEqual("bc", startInfo.FileName);
        Assert.AreEqual(2, startInfo.ArgumentList.Count);
        Assert.AreEqual("-e", startInfo.ArgumentList[0]);
        Assert.AreEqual("2 + 2", startInfo.ArgumentList[1]);
    }

    [TestMethod]
    public void RendersCommandsWithQuotesCorrectly()
    {
        var startInfo = new CommandLineTemplate("ollama run {{model}} \"Help me with this task: {{task}}\"").Render(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model"] = "deepseek-r1",
            ["task"] = "Write a story about a cat"
        });

        Assert.AreEqual("ollama", startInfo.FileName);
        Assert.AreEqual(3, startInfo.ArgumentList.Count);
        Assert.AreEqual("run", startInfo.ArgumentList[0]);
        Assert.AreEqual("deepseek-r1", startInfo.ArgumentList[1]);
        Assert.AreEqual("Help me with this task: Write a story about a cat", startInfo.ArgumentList[2]);
    }

    [TestMethod]
    public void RendersCommandsWithEnvironmentCorrectly()
    {
        var startInfo = new CommandLineTemplate("BC_ENV_ARGS=~/.bcrc bc -e {{expression}}").Render(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["expression"] = "2 + 2"
        });

        Assert.AreEqual("bc", startInfo.FileName);
        Assert.AreEqual(2, startInfo.ArgumentList.Count);
        Assert.AreEqual("-e", startInfo.ArgumentList[0]);
        Assert.AreEqual("2 + 2", startInfo.ArgumentList[1]);
        Assert.AreEqual("~/.bcrc", startInfo.Environment["BC_ENV_ARGS"]);
    }

    [TestMethod]
    public void RendersCommandsWithEnvironmentWithQuotesCorrectly()
    {
        var startInfo = new CommandLineTemplate("BC_ENV_ARGS=\"~/s p a c e/bcrc\" bc -e {{expression}}").Render(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["expression"] = "2 + 2"
        });

        Assert.AreEqual("bc", startInfo.FileName);
        Assert.AreEqual(2, startInfo.ArgumentList.Count);
        Assert.AreEqual("-e", startInfo.ArgumentList[0]);
        Assert.AreEqual("2 + 2", startInfo.ArgumentList[1]);
        Assert.AreEqual("~/s p a c e/bcrc", startInfo.Environment["BC_ENV_ARGS"]);
    }
}