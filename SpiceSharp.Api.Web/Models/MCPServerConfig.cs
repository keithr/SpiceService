namespace SpiceSharp.Api.Web.Models;

/// <summary>
/// Configuration for the MCP server
/// </summary>
public class MCPServerConfig
{
    /// <summary>
    /// Server name
    /// </summary>
    public string Name { get; set; } = "spicesharp-mcp-server";

    /// <summary>
    /// Server version
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// HTTP port for the server
    /// </summary>
    public int Port { get; set; } = 8081;

    /// <summary>
    /// Local IP address (auto-detected if not set)
    /// </summary>
    public string? LocalIp { get; set; }

    /// <summary>
    /// MCP protocol version
    /// </summary>
    public string ProtocolVersion { get; set; } = "2024-11-05";

    /// <summary>
    /// Paths to directories containing SPICE library (.lib) files
    /// </summary>
    public IEnumerable<string>? LibraryPaths { get; set; }
}

