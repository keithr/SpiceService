using SpiceSharp.Api.Web.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SpiceSharp.Api.Web.Services;

/// <summary>
/// Service for UDP discovery broadcasting
/// </summary>
public class DiscoveryService
{
    private readonly MCPServerConfig _config;
    private readonly MCPService _mcpService;
    private readonly ILogger<DiscoveryService> _logger;
    private readonly bool _networkVisible;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _broadcastTask;

    public DiscoveryService(MCPServerConfig config, MCPService mcpService, ILogger<DiscoveryService> logger, bool networkVisible = false)
    {
        _config = config;
        _mcpService = mcpService;
        _logger = logger;
        _networkVisible = networkVisible;
    }

    /// <summary>
    /// Start broadcasting discovery messages
    /// </summary>
    public async Task StartBroadcasting(DiscoveryConfig discoveryConfig)
    {
        if (!discoveryConfig.Enabled)
        {
            _logger.LogInformation("Discovery broadcasting is disabled");
            return;
        }

        _logger.LogInformation("Starting discovery broadcasting on UDP port {Port} with interval {Interval}s", 
            discoveryConfig.Port, discoveryConfig.IntervalSeconds);

        _cancellationTokenSource = new CancellationTokenSource();
        _broadcastTask = Task.Run(async () => 
        {
            try
            {
                await BroadcastLoop(discoveryConfig, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discovery broadcast loop terminated unexpectedly");
            }
        });
        
        // Give it a moment to start and verify it's running
        await Task.Delay(100);
        if (_broadcastTask.IsFaulted)
        {
            _logger.LogError("Discovery broadcast task failed to start");
        }
    }

    /// <summary>
    /// Stop broadcasting
    /// </summary>
    public void StopBroadcasting()
    {
        _logger.LogInformation("Stopping discovery broadcasting");
        _cancellationTokenSource?.Cancel();
        try
        {
            _broadcastTask?.Wait(TimeSpan.FromSeconds(5));
            _logger.LogInformation("Discovery broadcasting stopped");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping discovery broadcasting");
        }
    }

    private async Task BroadcastLoop(DiscoveryConfig discoveryConfig, CancellationToken cancellationToken)
    {
        UdpClient? udpClient = null;
        var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, discoveryConfig.Port);
        var consecutiveErrors = 0;
        const int maxConsecutiveErrors = 5;

        try
        {
            // Create UDP client and bind to any available port
            // Binding to port 0 lets the OS choose an available port
            udpClient = new UdpClient(0);
            udpClient.EnableBroadcast = true;
            
            _logger.LogInformation("UDP client created and bound for broadcasting");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var announcement = CreateAnnouncement();
                    var json = JsonSerializer.Serialize(announcement, new JsonSerializerOptions
                    {
                        WriteIndented = false
                    });
                    var bytes = Encoding.UTF8.GetBytes(json);

                    var sentBytes = await udpClient.SendAsync(bytes, bytes.Length, broadcastEndPoint);
                    
                    if (sentBytes > 0)
                    {
                        consecutiveErrors = 0;
                        var toolCount = announcement.ContainsKey("tools") && announcement["tools"] is Array toolArray 
                            ? toolArray.Length 
                            : 0;
                        // Extract port from announcement for logging
                        var announcementPort = _config.Port;
                        var announcementHost = _config.LocalIp ?? "127.0.0.1";
                        _logger.LogInformation("Discovery broadcast sent: {Bytes} bytes to {Endpoint} with {ToolCount} tools (announcing HTTP server at {Host}:{Port})", 
                            sentBytes, broadcastEndPoint, toolCount, announcementHost, announcementPort);
                    }
                    else
                    {
                        _logger.LogWarning("Discovery broadcast sent 0 bytes");
                    }
                    
                    await Task.Delay(TimeSpan.FromSeconds(discoveryConfig.IntervalSeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Discovery broadcast cancelled");
                    break;
                }
                catch (SocketException ex)
                {
                    consecutiveErrors++;
                    _logger.LogError(ex, "Socket error during discovery broadcast (attempt {Attempt}/{Max}): {ErrorCode}", 
                        consecutiveErrors, maxConsecutiveErrors, ex.SocketErrorCode);
                    
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        _logger.LogError("Too many consecutive errors ({Count}), attempting to recreate UDP client", 
                            consecutiveErrors);
                        
                        // Try to recreate the UDP client
                        try
                        {
                            udpClient?.Dispose();
                            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                            udpClient = new UdpClient(0);
                            udpClient.EnableBroadcast = true;
                            consecutiveErrors = 0;
                            _logger.LogInformation("UDP client recreated successfully");
                        }
                        catch (Exception recreateEx)
                        {
                            _logger.LogError(recreateEx, "Failed to recreate UDP client");
                            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                        }
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    _logger.LogError(ex, "Error during discovery broadcast (attempt {Attempt}/{Max})", 
                        consecutiveErrors, maxConsecutiveErrors);
                    
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        _logger.LogError("Too many consecutive errors, stopping broadcast loop");
                        break;
                    }
                    
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        finally
        {
            udpClient?.Dispose();
            _logger.LogInformation("Discovery broadcast loop ended");
        }
    }

    /// <summary>
    /// Create discovery announcement message
    /// </summary>
    public Dictionary<string, object> CreateAnnouncement()
    {
        if (_mcpService == null)
        {
            _logger.LogError("MCPService is null! Cannot create announcement.");
            throw new InvalidOperationException("MCPService is not initialized");
        }

        List<MCPToolDefinition> tools;
        try
        {
            tools = _mcpService.GetTools();
            _logger.LogInformation("MCPService.GetTools() returned {Count} tools", tools?.Count ?? 0);
            
            // Validate tools schema (GetTools already validates, but log if validation passes)
            if (tools != null && tools.Count > 0)
            {
                var validatedToolNames = tools.Select(t => t?.Name).Where(n => !string.IsNullOrEmpty(n)).ToArray();
                _logger.LogInformation("Tools schema validation passed for {Count} tools: {Tools}", 
                    validatedToolNames.Length, string.Join(", ", validatedToolNames));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tools from MCPService");
            tools = new List<MCPToolDefinition>();
        }

        var toolNames = tools?.Select(t => t?.Name).Where(n => !string.IsNullOrEmpty(n)).ToArray() ?? Array.Empty<string>();
        var instanceId = Guid.NewGuid().ToString();
        var processId = Environment.ProcessId;
        var announcementPort = _config.Port;
        // If network visibility is disabled, announce localhost only
        // Otherwise, announce the network IP (or localhost if no network IP available)
        var announcementHost = _networkVisible 
            ? (_config.LocalIp ?? "127.0.0.1")
            : "127.0.0.1";

        _logger.LogInformation("Creating discovery announcement with {Count} tools: {Tools}", 
            toolNames.Length, string.Join(", ", toolNames));
        _logger.LogInformation("Discovery announcement will include: host={Host}, port={Port}, path=/mcp (networkVisible={NetworkVisible})", 
            announcementHost, announcementPort, _networkVisible);

        return new Dictionary<string, object>
        {
            { "messageType", "mcp_server_announce" },
            { "version", _config.ProtocolVersion },
            { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00") },
            { "server", new Dictionary<string, object>
                {
                    { "name", _config.Name },
                    { "version", _config.Version },
                    { "transport", new Dictionary<string, object>
                        {
                            { "type", "http" },
                            { "host", announcementHost },
                            { "port", announcementPort },
                            { "path", "/mcp" }
                        }
                    },
                    { "network", new Dictionary<string, object>
                        {
                            { "local_ip", _config.LocalIp ?? "127.0.0.1" }
                        }
                    }
                }
            },
            { "service", new Dictionary<string, object>
                {
                    { "name", "SpiceSharp Circuit Simulator" },
                    { "version", _config.Version },
                    { "category", "circuit_simulation" },
                    { "status", "ready" },
                    { "capabilities", new[]
                        {
                            "dc_analysis",
                            "ac_analysis",
                            "transient_analysis",
                            "operating_point",
                            "parameter_sweep",
                            "temperature_sweep"
                        }
                    }
                }
            },
            { "tools", toolNames },
            { "instance", new Dictionary<string, object>
                {
                    { "name", $"{_config.Name}-{_config.LocalIp}" },
                    { "group", "default" },
                    { "id", instanceId },
                    { "pid", processId }
                }
            }
        };
    }
}

