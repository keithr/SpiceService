using Microsoft.Extensions.Logging;

namespace SpiceSharp.Api.Tray.Services;

/// <summary>
/// Logger that writes to a circular buffer
/// </summary>
public class LogBufferLogger : ILogger
{
    private readonly CircularLogBuffer _buffer;
    private readonly string _categoryName;

    public LogBufferLogger(CircularLogBuffer buffer, string categoryName)
    {
        _buffer = buffer;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var level = ConvertLogLevel(logLevel);
        
        _buffer.Add(level, $"[{_categoryName}] {message}", exception);
    }

    private static LogLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel level)
    {
        return level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => Services.LogLevel.Trace,
            Microsoft.Extensions.Logging.LogLevel.Debug => Services.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => Services.LogLevel.Information,
            Microsoft.Extensions.Logging.LogLevel.Warning => Services.LogLevel.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => Services.LogLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => Services.LogLevel.Critical,
            _ => Services.LogLevel.Information
        };
    }
}

public class LogBufferLoggerProvider : ILoggerProvider
{
    private readonly CircularLogBuffer _buffer;

    public LogBufferLoggerProvider(CircularLogBuffer buffer)
    {
        _buffer = buffer;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new LogBufferLogger(_buffer, categoryName);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

