namespace mcp0.Core;

internal sealed class DynamicPromptTemplate(string template)
{
    public string Render<T>(IReadOnlyDictionary<string, T> arguments)
    {
        return arguments.Count is 0 ? template : Template.Render(template, arguments);
    }
}