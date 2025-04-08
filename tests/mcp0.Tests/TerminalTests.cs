namespace mcp0;

[TestClass]
public sealed class TerminalTests
{
    [TestMethod]
    public void WrapsCorrectly()
    {
        var paragraph = "One very long line with a bigwordthatshouldwrap and a verybigwordthatsalittlebitlongerthanthewidthusedtowrap that wraps correctly.\n\nMagic!";
        var actual = Terminal.Wrap(paragraph, 40, 4);
        var expected =
        """
            One very long line with a
            bigwordthatshouldwrap and a verybigword\
            thatsalittlebitlongerthanthewidthusedto\
            wrap that wraps correctly.

            Magic!
        """;

        Assert.AreEqual(expected, actual);
    }
}