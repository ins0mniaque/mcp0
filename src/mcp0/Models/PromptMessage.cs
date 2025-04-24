using System.Globalization;

using mcp0.Core;

namespace mcp0.Models;

internal sealed record PromptMessage
{
    public required string Template { get; init; }
    public string? ReturnArgument { get; init; }
    public PromptOptions? Options { get; init; }

    public static PromptMessage Parse(string text)
    {
        return TryParse(text) ?? throw new FormatException($"Invalid prompt message: {text}");
    }

    public static PromptMessage? TryParse(string text)
    {
        text = text.Trim();
        if (text.Length is 0)
            return null;

        return new()
        {
            Options = TryParseOptions(Formattable.ParseAtEnd(ref text, " #")),
            ReturnArgument = Formattable.ParseAtEnd(ref text, "=>")?.TrimStart('{').TrimEnd('}'),
            Template = text
        };
    }

    private static PromptOptions? TryParseOptions(string? formatted)
    {
        if (formatted is null)
            return null;

        var models = new List<string>();
        var temperature = (float?)null;
        foreach (var option in formatted.Split(' ', StringSplitOptions.TrimEntries))
        {
            if (option.Length is 0)
                continue;
            if (option[^1] is '%' && float.TryParse(option[..^1], CultureInfo.InvariantCulture, out var percentage))
                temperature = percentage / 100f;
            else
                models.Add(option);
        }

        if (models.Count is 0 && temperature is null)
            return null;

        return new()
        {
            Model = models.ToArray(),
            Temperature = temperature
        };
    }

    public static string? TryFormat(PromptMessage message)
    {
        if (!TryFormat(message.Options, out var formattedOptions))
            return null;

        var formatted = message.Template;
        if (formattedOptions is not null)
            formatted = Formattable.FormatAtEnd(formatted, formattedOptions, " # ");
        if (message.ReturnArgument is not null)
            formatted = Formattable.FormatAtEnd(formatted, $"{{{{{message.ReturnArgument}}}}}", " => ");

        return formatted;
    }

    private static bool TryFormat(PromptOptions? options, out string? formatted)
    {
        formatted = null;
        if (options is null)
            return true;

        if (options.Context is not null || options.SystemPrompt is not null ||
            options.MaxTokens is not null || options.StopSequences is not null)
            return false;

        formatted = string.Join(' ', options.Model ?? []);
        if (options.Temperature is { } temperature)
            formatted += ' ' + temperature.ToString("P0", CultureInfo.InvariantCulture);

        return true;
    }
}