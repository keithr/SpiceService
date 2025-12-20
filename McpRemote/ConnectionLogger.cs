using System.Text;

namespace McpRemote;

/// <summary>
/// Thread-safe logger for MCP connection traffic.
/// Each connection gets its own log file identified by a unique connection ID.
/// </summary>
public class ConnectionLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
    private readonly string _connectionId;
    private bool _disposed = false;

    /// <summary>
    /// Gets the unique connection ID for this logger instance.
    /// </summary>
    public string ConnectionId => _connectionId;

    /// <summary>
    /// Gets the full path to the log file.
    /// </summary>
    public string LogFilePath => _logFilePath;

    /// <summary>
    /// Creates a new connection logger with a unique connection ID.
    /// </summary>
    /// <param name="logDirectory">Directory where log files will be stored. If null, uses a "logs" subdirectory in the executable directory.</param>
    public ConnectionLogger(string? logDirectory = null)
    {
        // Generate unique connection ID: ProcessId_Timestamp_Guid
        var processId = Environment.ProcessId;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var guid = Guid.NewGuid().ToString("N")[..8]; // Short GUID for readability
        _connectionId = $"{processId}_{timestamp}_{guid}";

        // Determine log directory
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            logDirectory = Path.Combine(exeDir, "logs");
        }

        // Ensure log directory exists
        Directory.CreateDirectory(logDirectory);

        // Create log file path
        _logFilePath = Path.Combine(logDirectory, $"McpRemote_{_connectionId}.log");

        // Open log file for appending (with auto-flush for immediate writes)
        _writer = new StreamWriter(_logFilePath, append: true, Encoding.UTF8)
        {
            AutoFlush = true
        };

        // Write initial log entry
        LogInfo($"=== MCP Connection Started ===");
        LogInfo($"Connection ID: {_connectionId}");
        LogInfo($"Process ID: {processId}");
        LogInfo($"Log File: {_logFilePath}");
        LogInfo($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
    }

    /// <summary>
    /// Logs a request received from stdin (client → server).
    /// </summary>
    public async Task LogRequestAsync(string requestJson)
    {
        await LogAsync("→ REQUEST", requestJson);
    }

    /// <summary>
    /// Logs a response sent to stdout (server → client).
    /// </summary>
    public async Task LogResponseAsync(string responseJson)
    {
        await LogAsync("← RESPONSE", responseJson);
    }

    /// <summary>
    /// Logs an HTTP request being sent to the server.
    /// </summary>
    public async Task LogHttpRequestAsync(string endpoint, string requestJson)
    {
        await LogAsync($"→ HTTP POST {endpoint}", requestJson);
    }

    /// <summary>
    /// Logs an HTTP response received from the server.
    /// </summary>
    public async Task LogHttpResponseAsync(int statusCode, string responseJson)
    {
        await LogAsync($"← HTTP {statusCode}", responseJson);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public async Task LogErrorAsync(string message, Exception? exception = null)
    {
        var errorText = $"ERROR: {message}";
        if (exception != null)
        {
            errorText += $"\nException: {exception.GetType().Name}: {exception.Message}";
            if (exception.StackTrace != null)
            {
                errorText += $"\nStack Trace: {exception.StackTrace}";
            }
        }
        await LogAsync("⚠ ERROR", errorText);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public void LogInfo(string message)
    {
        _ = LogAsync("ℹ INFO", message);
    }

    /// <summary>
    /// Core logging method that writes to the log file in a thread-safe manner.
    /// </summary>
    private async Task LogAsync(string direction, string content)
    {
        if (_disposed)
            return;

        await _writeLock.WaitAsync();
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            await _writer.WriteLineAsync($"[{timestamp}] {direction}");
            
            // Pretty-print JSON if possible, otherwise write as-is
            try
            {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                var formattedJson = System.Text.Json.JsonSerializer.Serialize(jsonDoc, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await _writer.WriteLineAsync(formattedJson);
            }
            catch
            {
                // Not JSON or invalid JSON - write as-is
                await _writer.WriteLineAsync(content);
            }
            
            await _writer.WriteLineAsync(); // Empty line separator
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Flushes any pending writes to disk.
    /// </summary>
    public async Task FlushAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            await _writer.FlushAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            LogInfo($"=== MCP Connection Ended ===");
            LogInfo($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _writer?.Flush();
            _writer?.Dispose();
        }
        catch
        {
            // Ignore errors during disposal
        }
        finally
        {
            _writeLock?.Dispose();
        }
    }
}
