using Microsoft.Extensions.Logging;

namespace mcp0;

internal static partial class Log
{
    public static LogLevel MinimumLevel { get; private set; } = LogLevel.Warning;

    public static void SetMinimumLevel(LogLevel level)
    {
        // TODO: Implement minimum level change at runtime through IConfigurationRoot.Reload
        MinimumLevel = level;
    }

    public static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(static logging =>
        {
            logging.SetMinimumLevel(MinimumLevel);

            // Send all logs to standard error because MCP uses standard output
            logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloading contexts: {Contexts}")]
    public static partial void ContextReloading(this ILogger logger, string[] contexts);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloaded contexts: {Contexts}")]
    public static partial void ContextReloaded(this ILogger logger, string[] contexts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to reload contexts: {Contexts}")]
    public static partial void ContextReloadFailed(this ILogger logger, Exception exception, string[] contexts);
}