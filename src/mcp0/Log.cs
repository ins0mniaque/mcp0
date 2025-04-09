using Microsoft.Extensions.Logging;

using ModelContextProtocol.Protocol.Types;

namespace mcp0;

internal static partial class Log
{
    private static LogLevel? level;
    public static LogLevel? Level
    {
        get => level;
        set
        {
            // TODO: Implement minimum level change at runtime through IConfigurationRoot.Reload
            level = value;
        }
    }

    public static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(static logging =>
        {
            if (Level is { } minimumLevel)
                logging.SetMinimumLevel(minimumLevel);

            // Send all logs to standard error because MCP uses standard output
            logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        });
    }

    public static LogLevel ToLogLevel(this LoggingLevel loggingLevel) => loggingLevel switch
    {
        LoggingLevel.Debug => LogLevel.Debug,
        LoggingLevel.Info => LogLevel.Information,
        LoggingLevel.Notice => LogLevel.Information,
        LoggingLevel.Warning => LogLevel.Warning,
        LoggingLevel.Error => LogLevel.Error,
        LoggingLevel.Critical => LogLevel.Critical,
        LoggingLevel.Alert => LogLevel.Critical,
        LoggingLevel.Emergency => LogLevel.Critical,
        _ => LogLevel.None
    };

    public static LoggingLevel ToLoggingLevel(this LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => LoggingLevel.Debug,
        LogLevel.Debug => LoggingLevel.Debug,
        LogLevel.Information => LoggingLevel.Info,
        LogLevel.Warning => LoggingLevel.Warning,
        LogLevel.Error => LoggingLevel.Error,
        LogLevel.Critical => LoggingLevel.Critical,
        _ => LoggingLevel.Emergency
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloading contexts: {Contexts}")]
    public static partial void ContextReloading(this ILogger logger, string[] contexts);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloaded contexts: {Contexts}")]
    public static partial void ContextReloaded(this ILogger logger, string[] contexts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to reload contexts: {Contexts}")]
    public static partial void ContextReloadFailed(this ILogger logger, Exception exception, string[] contexts);
}