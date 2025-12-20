namespace SpiceSharp.Api.Tray.Models;

/// <summary>
/// Represents a detected IDE with its configuration information
/// </summary>
public class DetectedIDE
{
    /// <summary>
    /// Display name of the IDE
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Full path to the configuration file
    /// </summary>
    public string ConfigFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the IDE is installed (detected)
    /// </summary>
    public bool IsInstalled { get; set; }
    
    /// <summary>
    /// Whether this IDE is selected for configuration
    /// </summary>
    public bool IsSelected { get; set; }
    
    /// <summary>
    /// Whether this IDE requires manual configuration (e.g., VS Code)
    /// </summary>
    public bool RequiresManualSetup { get; set; }
}

