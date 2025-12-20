namespace SpiceSharp.Api.Tray.Models;

/// <summary>
/// Input parameters for IDE configuration dialog
/// </summary>
public class IDEConfigurationInput
{
    /// <summary>
    /// The complete MCP endpoint URL (e.g., "http://localhost:8081/mcp")
    /// Parent app reads this from MCPServerConfig at runtime
    /// </summary>
    public string McpEndpointUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Full path to McpRemote.exe proxy executable
    /// Typically in same directory as SpiceService.exe
    /// </summary>
    public string ProxyExecutablePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional: Server status to validate before configuration
    /// </summary>
    public bool IsServerRunning { get; set; }
}

