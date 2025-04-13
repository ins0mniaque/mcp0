using System.CommandLine.Parsing;

namespace mcp0.Core;

internal static class CommandLine
{
    public static string[] Split(string commandLine)
    {
        return CommandLineStringSplitter.Instance.Split(commandLine).ToArray();
    }

    public static void Split(string commandLine, out string? command, out string[]? arguments)
    {
        arguments = Split(commandLine);

        command = arguments.Length is 0 ? null : arguments[0];
        arguments = arguments.Length is 0 ? null : arguments[1..];
    }

    public static void Split(string commandLine, out string? command, out string[]? arguments, IDictionary<string, string> environment)
    {
        Split(Split(commandLine), out command, out arguments, environment);
    }

    public static void Split(string[] commandLine, out string? command, out string[]? arguments, IDictionary<string, string> environment)
    {
        var commandIndex = ParseEnvironment(commandLine, environment);

        command = commandLine.Length is 0 ? null : commandLine[commandIndex];
        arguments = commandLine.Length is 0 ? null : commandLine[(commandIndex + 1)..];
    }

    public static string? ParseComment(ref string commandLine)
    {
        var index = commandLine.AsSpan().LastIndexOf(" #", StringComparison.Ordinal);
        if (index is -1)
            return null;

        var comment = commandLine[(index + 2)..].Trim();
        commandLine = commandLine[..index].Trim();
        return comment;
    }

    public static int ParseEnvironment(string[] commandLine, IDictionary<string, string> environment)
    {
        var commandIndex = -1;
        var keyValueRanges = (Span<Range>)stackalloc Range[2];

        while (++commandIndex < commandLine.Length)
        {
            var keyValue = commandLine[commandIndex].AsSpan();
            if (!DotEnv.Split(keyValue, keyValueRanges))
                break;

            var key = keyValue[keyValueRanges[0]];
            var value = keyValue[keyValueRanges[1]];
            if (!DotEnv.IsValidKey(key))
                break;

            environment[key.ToString()] = value.ToString();
        }

        return commandIndex;
    }
}