namespace SpiceSharp.Api.Tray.Models;

/// <summary>
/// Result of IDE configuration operation
/// </summary>
public class ConfigurationResult
{
    /// <summary>
    /// Timestamp when configuration was executed
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// List of IDE names that were successfully configured
    /// </summary>
    public List<string> ConfiguredIDEs { get; set; } = new();
    
    /// <summary>
    /// List of backup file paths created (formatted as "IDE Name: path")
    /// </summary>
    public List<string> BackupPaths { get; set; } = new();
    
    /// <summary>
    /// List of errors encountered (formatted as "IDE Name: error message")
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// VS Code configuration JSON (if VS Code was selected)
    /// </summary>
    public string? VSCodeConfigJson { get; set; }
}

