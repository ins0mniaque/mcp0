using System.CommandLine;

var rootCommand = new RootCommand("mcp0 - Secure MCP (Model Context Protocol) servers configurator/inspector");

rootCommand.AddCommand(new RunCommand());

await rootCommand.InvokeAsync(args);
