using System.CommandLine;
using System.CommandLine.Invocation;

namespace mcp0.Commands;

internal static class SymbolExtensions
{
    public static T GetValue<T>(this Argument<T> argument, InvocationContext context)
    {
        return context.ParseResult.GetValueForArgument(argument);
    }

    public static T? GetValue<T>(this Option<T> option, InvocationContext context)
    {
        return context.ParseResult.GetValueForOption(option);
    }

    public static T? GetValue<T>(this Option<T> option, string environmentVariable, InvocationContext context)
    {
        if (context.ParseResult.FindResultFor(option) is { } result && result.Tokens.Count is not 0)
            return result.GetValueForOption(option);

        if (Environment.GetEnvironmentVariable(environmentVariable) is { } value)
            return option.Parse([option.Aliases.First(), value]).GetValueForOption(option);

        return option.Parse([]).GetValueForOption(option);
    }

    public static T GetRequiredValue<T>(this Option<T> option, InvocationContext context)
    {
        return GetValue(option, context) ??
               throw new CommandLineConfigurationException($"Option {option.Name} is required");
    }

    public static T GetRequiredValue<T>(this Option<T> option, string environmentVariable, InvocationContext context)
    {
        return GetValue(option, environmentVariable, context) ??
               throw new CommandLineConfigurationException($"Option {option.Name} or {environmentVariable} environment variable is required");
    }
}