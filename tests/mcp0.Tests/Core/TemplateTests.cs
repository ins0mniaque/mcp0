namespace mcp0.Core;

[TestClass]
public sealed class TemplateTests
{
    [TestMethod]
    public void ParsesArgumentCorrectly()
    {
        var arguments = Template.Parse("{{argument}}");
        var expected = new Template.Argument("argument", null, true);

        Assert.AreEqual(1, arguments.Length);
        Assert.AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesRequiredCorrectly()
    {
        var arguments = Template.Parse("{{argument?}}");
        var expected = new Template.Argument("argument", null, false);

        Assert.AreEqual(1, arguments.Length);
        Assert.AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesDescriptionCorrectly()
    {
        var arguments = Template.Parse("{{argument:desc}}");
        var expected = new Template.Argument("argument", "desc", true);

        Assert.AreEqual(1, arguments.Length);
        Assert.AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesTemplateCorrectly()
    {
        var template =
        """
        These are arguments: {{argument}} {{_underscore}} {{numbered0}} {{optional?}} {{described?:desc}}.
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        var arguments = Template.Parse(template);
        var expected = new Template.Argument[]
        {
            new("argument", null, true),
            new("_underscore", null, true),
            new("numbered0", null, true),
            new("optional", null, false),
            new("described", "desc", false)
        };

        Assert.AreEqual(5, arguments.Length);
        for (var index = 0; index < expected.Length; index++)
            Assert.AreEqual(expected[index], arguments[index]);
    }

    [TestMethod]
    public void RendersTemplateCorrectly()
    {
        var template =
        """
        These are arguments: {{argument}} {{_underscore}} {{numbered0}} {{optional?}} {{described?:desc}}.
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        var actual = Template.Render(template, new Dictionary<string, string>
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
}