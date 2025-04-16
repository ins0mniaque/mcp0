using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

using mcp0.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace mcp0.Commands;

internal sealed class Root : RootCommand
{
    public Root(IEnumerable<Command> commands)
    {
        AddGlobalOption(LogLevelOption);
        foreach (var command in commands)
            AddCommand(command);
    }

    private static Option<LogLevel?> LogLevelOption { get; } = new("--loglevel");

    public static void ConfigureCommandLine(CommandLineBuilder commandLine, IServiceProvider serviceProvider)
    {
        commandLine.AddMiddleware(LogLevelMiddleware)
                   .UseDefaults()
                   .UseHelp(Help);
    }

    private static async Task LogLevelMiddleware(InvocationContext context, Func<InvocationContext, Task> next)
    {
        var serviceProvider = context.BindingContext.GetRequiredService<IServiceProvider>();
        var configurationRoot = serviceProvider.GetService<IConfigurationRoot>();

        configurationRoot?.SetLogLevel(context.ParseResult.GetValueForOption(LogLevelOption));

        await next(context);
    }

    private static void Help(HelpContext context)
    {
        const string Banner = "mcp0 - Secure MCP (Model Context Protocol) servers configurator/inspector/proxy";

        context.HelpBuilder.CustomizeLayout(static context =>
            HelpBuilder.Default.GetLayout()
                .Skip(string.IsNullOrEmpty(context.Command.Description) ? 1 : 0)
                .Prepend(static _ => Terminal.WriteLine(Banner)));

        context.HelpBuilder.CustomizeSymbol(LogLevelOption,
            firstColumnText: "--loglevel <level>",
            secondColumnText: "Minimum severity logging level\n<level: Trace|Debug|Information|Warning|Error|Critical|None>");
    }
}