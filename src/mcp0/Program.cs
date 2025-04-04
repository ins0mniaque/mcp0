﻿using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;

var logLevelOption = new Option<LogLevel>("--loglevel", () => LogLevel.Warning, "Minimum severity logging level");

var rootCommand = new RootCommand("mcp0 - Secure MCP (Model Context Protocol) servers configurator/inspector")
{
    new RunCommand(),
    new ToolsCommand()
};

rootCommand.AddGlobalOption(logLevelOption);

var parsed = rootCommand.Parse(args);

Logging.MinimumLevel = parsed.GetValueForOption(logLevelOption);

await parsed.InvokeAsync();
