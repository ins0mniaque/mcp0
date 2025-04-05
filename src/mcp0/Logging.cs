using Microsoft.Extensions.Logging;

internal static class Logging
{
    public static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(ConfigureLogging);
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    }
}
