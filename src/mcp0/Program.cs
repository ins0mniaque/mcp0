using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;

var logLevelOption = new Option<LogLevel>("--loglevel", () => LogLevel.Warning, "Minimum severity logging level");

var rootCommand = new RootCommand("mcp0 - Secure MCP (Model Context Protocol) servers configurator/inspector")
{
    new InspectCommand(),
    new RunCommand()
};

rootCommand.AddGlobalOption(logLevelOption);

var parsed = rootCommand.Parse(args);

Log.MinimumLevel = parsed.GetValueForOption(logLevelOption);

return await rootCommand.InvokeAsync(args);
