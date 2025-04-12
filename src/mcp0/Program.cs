using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

using mcp0;
using mcp0.Commands;

using Microsoft.Extensions.Logging;

var rootCommand = new RootCommand()
{
    new InspectCommand(),
    new RunCommand(),
    new ShellCommand()
};

var logLevelOption = new Option<LogLevel?>("--loglevel");

rootCommand.AddGlobalOption(logLevelOption);

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseHelp(context =>
    {
        const string Title = "mcp0 - Secure MCP (Model Context Protocol) servers configurator/inspector/proxy";

        context.HelpBuilder.CustomizeLayout(static _ =>
            HelpBuilder.Default.GetLayout().Skip(1).Prepend(static _ => Terminal.WriteLine(Title)));
        context.HelpBuilder.CustomizeSymbol(logLevelOption,
            firstColumnText: "--loglevel <level>",
            secondColumnText: "Minimum severity logging level: <Trace|Debug|Information|Warning|Error|Critical|None>");
    })
    .Build();

var parsed = parser.Parse(args);

Log.Level = parsed.GetValueForOption(logLevelOption);

return await parsed.InvokeAsync();