namespace mcp0.Core;

[TestClass]
public sealed class DotEnvTests
{
    [TestMethod]
    public void ParsesKeyValueCorrectly()
    {
        var env = DotEnv.Parse("KEY=VALUE");

        Assert.AreEqual(1, env.Count);
        Assert.AreEqual("VALUE", env["KEY"]);
    }

    [TestMethod]
    public void ParsesCommentsCorrectly()
    {
        var env = DotEnv.Parse("# KEY=COMMENT\nKEY=VALUE");

        Assert.AreEqual(1, env.Count);
        Assert.AreEqual("VALUE", env["KEY"]);
    }

    [TestMethod]
    public void ParsesEmptyLinesCorrectly()
    {
        var env = DotEnv.Parse("\n\n\nKEY=VALUE");

        Assert.AreEqual(1, env.Count);
        Assert.AreEqual("VALUE", env["KEY"]);
    }

    [TestMethod]
    public void ParsesEqualSignsInValuesCorrectly()
    {
        var env = DotEnv.Parse("\n\n\nKEY=VALUE=EQUAL");

        Assert.AreEqual(1, env.Count);
        Assert.AreEqual("VALUE=EQUAL", env["KEY"]);
    }

    [TestMethod]
    public void ParsesNonVariableLinesCorrectly()
    {
        var env = DotEnv.Parse("KEY=VALUE\n\ndocker run -e KEY=DOCKER_KEY");

        Assert.AreEqual(1, env.Count);
        Assert.AreEqual("VALUE", env["KEY"]);
    }

    [TestMethod]
    public void ParsesMultipleLinesCorrectly()
    {
        var env = DotEnv.Parse("KEY=VALUE\nKEY2=VALUE2");

        Assert.AreEqual(2, env.Count);
        Assert.AreEqual("VALUE", env["KEY"]);
        Assert.AreEqual("VALUE2", env["KEY2"]);
    }
}