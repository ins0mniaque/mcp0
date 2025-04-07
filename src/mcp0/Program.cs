using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;

var rootCommand = new RootCommand()
{
    new InspectCommand(),
    new RunCommand()
};

var logLevelOption = new Option<LogLevel>("--loglevel", static () => LogLevel.Warning);

rootCommand.AddGlobalOption(logLevelOption);

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseHelp(ctx =>
    {
        const string title = "mcp0 - Secure MCP (Model Context Protocol) servers configurator/inspector";

        ctx.HelpBuilder.CustomizeLayout(static _ =>
            HelpBuilder.Default.GetLayout().Skip(1).Prepend(static _ => Terminal.WriteLine(title)));
        ctx.HelpBuilder.CustomizeSymbol(logLevelOption,
            firstColumnText: "--loglevel <level>",
            secondColumnText: "Minimum severity logging level: <Trace|Debug|Information|Warning|Error|Critical>");
    })
    .Build();

var parsed = parser.Parse(args);

Log.SetMinimumLevel(parsed.GetValueForOption(logLevelOption));

return await parsed.InvokeAsync();
