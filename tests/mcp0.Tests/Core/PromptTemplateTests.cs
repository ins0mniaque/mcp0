using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

[TestClass]
public sealed class PromptTemplateTests
{
    [TestMethod]
    public void ParsesArgumentCorrectly()
    {
        var arguments = PromptTemplate.Parse("{{argument}}");
        var expected = new PromptArgument { Name = "argument", Description = null, Required = true };

        Assert.AreEqual(1, arguments.Count);
        AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesRequiredCorrectly()
    {
        var arguments = PromptTemplate.Parse("{{argument?}}");
        var expected = new PromptArgument { Name = "argument", Description = null };

        Assert.AreEqual(1, arguments.Count);
        AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesDescriptionCorrectly()
    {
        var arguments = PromptTemplate.Parse("{{argument#desc}}");
        var expected = new PromptArgument { Name = "argument", Description = "desc", Required = true };

        Assert.AreEqual(1, arguments.Count);
        AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesTemplateCorrectly()
    {
        var template =
        """
        These are arguments: {{argument}} {{_underscore}} {{numbered0}} {{optional?}} {{described?#desc}}.
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        var arguments = PromptTemplate.Parse(template);
        var expected = new PromptArgument[]
        {
            new() { Name = "argument", Description = null, Required = true },
            new() { Name = "_underscore", Description = null, Required = true },
            new() { Name = "numbered0", Description = null, Required = true },
            new() { Name = "optional", Description = null },
            new() { Name = "described", Description = "desc" }
        };

        Assert.AreEqual(5, arguments.Count);
        for (var index = 0; index < expected.Length; index++)
            AreEqual(expected[index], arguments[index]);
    }

    [TestMethod]
    public void RendersTemplateCorrectly()
    {
        var template =
        """
        These are arguments: {{argument}} {{_underscore}} {{numbered0}} {{optional?}} {{described?#desc}}.
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        var actual = Template.Render(template, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "argument", "value" },
            { "optional", "option" }
        });

        var expected =
        """
        These are arguments: value {{_underscore}} {{numbered0}} option .
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        Assert.AreEqual(expected, actual);
    }

    private static void AreEqual(PromptArgument expected, PromptArgument actual)
    {
        Assert.AreEqual(expected.Name, actual.Name);
        Assert.AreEqual(expected.Description, actual.Description);
        Assert.AreEqual(expected.Required, actual.Required);
    }
}