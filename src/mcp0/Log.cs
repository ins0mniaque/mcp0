using Microsoft.Extensions.Logging;

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
}
