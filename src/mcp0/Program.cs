using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

using mcp0.Commands;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
var services = new ServiceCollection();

services.AddSingleton(configuration);
services.AddSingleton<IConfiguration>(configuration);

services.AddLogging(logging =>
{
    logging.AddConfiguration(configuration);

    // Send all logs to standard error because MCP uses standard output
    logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);
});

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