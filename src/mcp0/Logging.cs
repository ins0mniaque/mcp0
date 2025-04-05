using Microsoft.Extensions.Logging;

internal static class Logging
{
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Warning;

    public static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(MinimumLevel);

            // Send all logs to standard error because MCP uses standard output
            logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        });
    }
}
