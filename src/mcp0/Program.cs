using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

using mcp0;
using mcp0.Commands;

using Microsoft.Extensions.Logging;

var rootCommand = new RootCommand
{
    new InspectCommand(),
    new NewCommand(),
    new RunCommand(),
    new ServeCommand(),
    new ShellCommand()
};

var logLevelOption = new Option<LogLevel?>("--loglevel");

rootCommand.AddGlobalOption(logLevelOption);

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseHelp(Customize)
    .Build();

var parsed = parser.Parse(args);

Log.Level = parsed.GetValueForOption(logLevelOption);

return await parsed.InvokeAsync();

void Customize(HelpContext context)
{
    const string Banner = "mcp0 - Secure MCP (Model Context Protocol) servers configurator/inspector/proxy";

    context.HelpBuilder.CustomizeLayout(static context =>
        HelpBuilder.Default.GetLayout()
            .Skip(string.IsNullOrEmpty(context.Command.Description) ? 1 : 0)
            .Prepend(static _ => Terminal.WriteLine(Banner)));

    context.HelpBuilder.CustomizeSymbol(logLevelOption,
        firstColumnText: "--loglevel <level>",
        secondColumnText: "Minimum severity logging level\n<level: Trace|Debug|Information|Warning|Error|Critical|None>");
}