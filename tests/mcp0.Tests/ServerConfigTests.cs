namespace mcp0.Tests;

[TestClass]
public sealed class ServerConfigTests
{
    [TestMethod]
    public void TestMethod1()
    {
        var config = new ServerConfig();

        Assert.IsNotNull(config);
    }
}
