using mcp0.Mcp;

using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

[TestClass]
public sealed class DynamicPromptTests
{
    private const string ModelResponse = "MODEL RESPONSE";

    [TestMethod]
    public void ParsesArgumentCorrectly()
    {
        var prompt = new Models.Prompt { Messages = [new() { Template = "{{argument}}" }] };
        var arguments = new DynamicPrompt(string.Empty, prompt).Prompt.Arguments;
        var expected = new PromptArgument { Name = "argument", Description = null, Required = true };

        Assert.IsNotNull(arguments);
        Assert.AreEqual(1, arguments.Count);
        AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesRequiredCorrectly()
    {
        var prompt = new Models.Prompt { Messages = [new() { Template = "{{argument?}}" }] };
        var arguments = new DynamicPrompt(string.Empty, prompt).Prompt.Arguments;
        var expected = new PromptArgument { Name = "argument", Description = null };

        Assert.IsNotNull(arguments);
        Assert.AreEqual(1, arguments.Count);
        AreEqual(expected, arguments[0]);
    }

    [TestMethod]
    public void ParsesDescriptionCorrectly()
    {
        var prompt = new Models.Prompt { Messages = [new() { Template = "{{argument#desc}}" }] };
        var arguments = new DynamicPrompt(string.Empty, prompt).Prompt.Arguments;
        var expected = new PromptArgument { Name = "argument", Description = "desc", Required = true };

        Assert.IsNotNull(arguments);
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

        var prompt = new Models.Prompt { Messages = [new() { Template = template }] };
        var arguments = new DynamicPrompt(string.Empty, prompt).Prompt.Arguments;
        var expected = new PromptArgument[]
        {
            new() { Name = "argument", Description = null, Required = true },
            new() { Name = "_underscore", Description = null, Required = true },
            new() { Name = "numbered0", Description = null, Required = true },
            new() { Name = "optional", Description = null },
            new() { Name = "described", Description = "desc" }
        };

        Assert.IsNotNull(arguments);
        Assert.AreEqual(5, arguments.Count);
        for (var index = 0; index < expected.Length; index++)
            AreEqual(expected[index], arguments[index]);
    }

    [TestMethod]
    public async Task RendersTemplateCorrectly()
    {
        var template =
        """
        These are arguments: {{argument}} {{_underscore}} {{numbered0}} {{optional?}} {{described?#desc}}.
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        await using var server = new McpSamplingServer(static _ => ModelResponse);

        var prompt = new Models.Prompt { Messages = [new() { Template = template }] };
        var actual = await new DynamicPromptTemplate(prompt).Render(server, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["argument"] = "value",
            ["optional"] = "option"
        }, CancellationToken.None);

        var expected =
        """
        These are arguments: <argument>value</argument> {{_underscore}} {{numbered0}} <optional>option</optional> .
        These are not arguments: {{}} {{0}} {{ not_argument }} {{0argument}} {{\"escaped\"}}.
        """;

        Assert.AreEqual(1, actual.Count);
        Assert.AreEqual(Role.User, actual[0].Role);
        Assert.AreEqual(expected, actual[0].Content.Text);
    }

    [TestMethod]
    public async Task RendersDynamicTemplateCorrectly()
    {
        await using var server = new McpSamplingServer(static _ => ModelResponse);

        var document = "This is a multi-line document.\nIt contains multiple lines.\nAnd some text.";
        var prompt = new Models.Prompt
        {
            Messages =
            [
                new() { Template = "Summarize this document: {{document}}", ReturnArgument = "summary" },
                new() { Template = "Given this document: {{document}} and summary: {{summary}}, provide feedback on the summary.", ReturnArgument = "feedback" },
                new() { Template = "Given this document: {{document}}, summary: {{summary}} and feedback: {{feedback}}, update on the summary based on the feedback.", ReturnArgument = string.Empty }
            ]
        };

        var actual = await new DynamicPromptTemplate(prompt).Render(server, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["document"] = document
        }, CancellationToken.None);

        Assert.AreEqual(6, actual.Count);
        Assert.AreEqual(Role.User, actual[0].Role);
        Assert.AreEqual(Role.Assistant, actual[1].Role);
        Assert.AreEqual(Role.User, actual[2].Role);
        Assert.AreEqual(Role.Assistant, actual[3].Role);
        Assert.AreEqual(Role.User, actual[4].Role);
        Assert.AreEqual(Role.Assistant, actual[5].Role);
        Assert.AreEqual($"Summarize this document: \n<document>\n{document}\n</document>\n", actual[0].Content.Text);
        Assert.AreEqual(ModelResponse, actual[1].Content.Text);
        Assert.AreEqual($"Given this document: \n<document>\n{document}\n</document>\n and summary: <summary>{ModelResponse}</summary>, provide feedback on the summary.", actual[2].Content.Text);
        Assert.AreEqual(ModelResponse, actual[3].Content.Text);
        Assert.AreEqual($"Given this document: \n<document>\n{document}\n</document>\n, summary: <summary>{ModelResponse}</summary> and feedback: <feedback>{ModelResponse}</feedback>, update on the summary based on the feedback.", actual[4].Content.Text);
        Assert.AreEqual(ModelResponse, actual[5].Content.Text);
    }

    private static void AreEqual(PromptArgument expected, PromptArgument actual)
    {
        Assert.AreEqual(expected.Name, actual.Name);
        Assert.AreEqual(expected.Description, actual.Description);
        Assert.AreEqual(expected.Required, actual.Required);
    }
}