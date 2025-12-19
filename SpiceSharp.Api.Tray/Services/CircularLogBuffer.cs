using System.Collections.Concurrent;

namespace SpiceSharp.Api.Tray.Services;

/// <summary>
/// Thread-safe circular buffer for log entries
/// </summary>
public class CircularLogBuffer
{
    private readonly ConcurrentQueue<LogEntry> _buffer;
    private readonly int _maxSize;
    private int _currentSize;

    public CircularLogBuffer(int maxSize = 1000)
    {
        _maxSize = maxSize;
        _buffer = new ConcurrentQueue<LogEntry>();
        _currentSize = 0;
    }

    /// <summary>
    /// Add a log entry to the buffer
    /// </summary>
    public void Add(LogLevel level, string message, Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Exception = exception?.ToString()
        };

        _buffer.Enqueue(entry);
        Interlocked.Increment(ref _currentSize);

        // Remove oldest entries if buffer is full
        while (_currentSize > _maxSize && _buffer.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _currentSize);
        }
    }

    /// <summary>
    /// Get all log entries
    /// </summary>
    public IEnumerable<LogEntry> GetEntries()
    {
        return _buffer.ToArray();
    }

    /// <summary>
    /// Clear all log entries
    /// </summary>
    public void Clear()
    {
        while (_buffer.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _currentSize);
        }
    }

    /// <summary>
    /// Get the current number of entries
    /// </summary>
    public int Count => _currentSize;
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }

    public override string ToString()
    {
        var levelStr = Level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "     "
        };

        var timeStr = Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var result = $"[{timeStr}] [{levelStr}] {Message}";
        
        if (!string.IsNullOrEmpty(Exception))
        {
            result += $"\n{Exception}";
        }

        return result;
    }
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

