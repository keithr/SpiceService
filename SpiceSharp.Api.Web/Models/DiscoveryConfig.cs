namespace SpiceSharp.Api.Web.Models;

/// <summary>
/// Configuration for discovery service
/// </summary>
public class DiscoveryConfig
{
    /// <summary>
    /// Whether discovery broadcasting is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// UDP port for discovery broadcasts
    /// </summary>
    public int Port { get; set; } = 19847;

    /// <summary>
    /// Broadcast interval in seconds
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;
}

