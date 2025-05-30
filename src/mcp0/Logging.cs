using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Protocol.Types;

namespace mcp0;

internal static class Logging
{
    public static LogLevel? GetLogLevel(this IConfiguration configuration)
    {
        return Enum.TryParse<LogLevel>(configuration["LogLevel:Default"], out var logLevel) ? logLevel : null;
    }

    public static void SetLogLevel(this IConfigurationRoot configurationRoot, LogLevel? logLevel)
    {
        configurationRoot["LogLevel:Default"] = logLevel?.ToString();
        configurationRoot.Reload();
    }

    public static void TrySetLogLevel(this IConfigurationRoot configurationRoot, LogLevel? logLevel)
    {
        if (configurationRoot["LogLevel:Default"] is null)
            configurationRoot.SetLogLevel(logLevel);
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
}