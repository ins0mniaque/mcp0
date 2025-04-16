using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

using mcp0.Commands;

using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton<RootCommand, Root>();
services.AddSingleton(Root.ConfigureCommandLine);
services.AddSingleton<Command, InspectCommand>();
services.AddSingleton<Command, NewCommand>();
services.AddSingleton<Command, RunCommand>();
services.AddSingleton<Command, ServeCommand>();
services.AddSingleton<Command, ShellCommand>();

await using var serviceProvider = services.BuildServiceProvider();

var rootCommand = serviceProvider.GetRequiredService<RootCommand>();
var commandLine = new CommandLineBuilder(rootCommand).UseServiceProvider(serviceProvider);

return await commandLine.Build().InvokeAsync(args);