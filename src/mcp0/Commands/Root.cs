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
    public const string Banner = "mcp0 - Secure MCP (Model Context Protocol) servers configurator/inspector/proxy";

    public Root(IEnumerable<Command> commands)
    {
        AddGlobalOption(SamplingOption);
        AddGlobalOption(SamplingEndpointOption);
        AddGlobalOption(SamplingApiKeyOption);
        AddGlobalOption(SamplingModelOption);
        AddGlobalOption(LogLevelOption);
        foreach (var command in commands)
            AddCommand(command);
    }

    private enum SamplingProvider { Default, Ollama, OpenAI }

    private static Option<SamplingProvider> SamplingOption { get; } = new("--sampling", "Set the sampling provider");
    private static Option<Uri?> SamplingEndpointOption { get; } = new("--sampling-endpoint", "Set the sampling provider endpoint");
    private static Option<string?> SamplingApiKeyOption { get; } = new("--sampling-api-key", "Set the sampling provider API key");
    private static Option<string?> SamplingModelOption { get; } = new("--sampling-model", "Set the sampling provider default model");
    private static Option<LogLevel?> LogLevelOption { get; } = new("--loglevel");

    public static void ConfigureCommandLine(CommandLineBuilder commandLine, IServiceProvider serviceProvider)
    {
        commandLine.AddMiddleware(SamplingMiddleware)
                   .AddMiddleware(LogLevelMiddleware)
                   .CancelOnProcessTermination()
                   .RegisterWithDotnetSuggest()
                   .UseEnvironmentVariableDirective()
                   .UseExceptionHandler(HandleException)
                   .UseHelp(Help)
                   .UseParseDirective()
                   .UseParseErrorReporting()
                   .UseSuggestDirective()
                   .UseTypoCorrections()
                   .UseVersionOption();
    }

    private static async Task SamplingMiddleware(InvocationContext context, Func<InvocationContext, Task> next)
    {
        var serviceProvider = context.GetServiceProvider();
        var sampling = serviceProvider.GetService<Sampling>();
        var samplingProvider = SamplingOption.GetValue(context);

        if (samplingProvider is SamplingProvider.Ollama)
            sampling?.ConfigureOllama(SamplingEndpointOption.GetValue(context),
                                      SamplingModelOption.GetValue(context));
        else if (samplingProvider is SamplingProvider.OpenAI)
            sampling?.ConfigureOpenAI(SamplingEndpointOption.GetValue(context),
                                      SamplingApiKeyOption.GetValue(context),
                                      SamplingModelOption.GetValue(context));
        else
            sampling?.ConfigureDefault();

        await next(context);
    }

    private static async Task LogLevelMiddleware(InvocationContext context, Func<InvocationContext, Task> next)
    {
        var serviceProvider = context.GetServiceProvider();
        var configurationRoot = serviceProvider.GetService<IConfigurationRoot>();

        configurationRoot?.SetLogLevel(LogLevelOption.GetValue(context));

        await next(context);
    }

    private static void Help(HelpContext context)
    {
        context.HelpBuilder.CustomizeLayout(static context =>
            HelpBuilder.Default.GetLayout()
                .Skip(string.IsNullOrEmpty(context.Command.Description) ? 1 : 0)
                .Prepend(static _ => Terminal.WriteLine(Banner)));

        context.HelpBuilder.CustomizeSymbol(LogLevelOption,
            firstColumnText: "--loglevel <level>",
            secondColumnText: "Minimum severity logging level\n<level: Trace|Debug|Information|Warning|Error|Critical|None>");
    }

    private static void HandleException(Exception exception, InvocationContext context)
    {
        if (exception is not OperationCanceledException)
            Terminal.WriteLine(exception.Message, ConsoleColor.Red);

        context.ExitCode = 1;
    }
}