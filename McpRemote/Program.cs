using System.Text;
using System.Text.Json;

namespace McpRemote;

class Program
{
    static async Task<int> Main(string[] args)
    {
#if ENABLE_LOGGING
        // Initialize logger - get log directory from environment variable or use default
        var logDirectory = Environment.GetEnvironmentVariable("MCPREMOTE_LOG_DIR");
        using var logger = new ConnectionLogger(logDirectory);
#else
        ConnectionLogger? logger = null;
#endif
        
        string httpEndpoint;
        
        // 1. Parse arguments - support both explicit URL and auto-discovery
        if (args.Length == 0 || args[0] == "auto" || args[0] == "--discover")
        {
            // Auto-discovery mode: try to find SpiceService on common ports
            var discoveredEndpoint = await DiscoverEndpointAsync(logger);
            if (discoveredEndpoint == null)
            {
                await Console.Error.WriteLineAsync("Error: Could not discover SpiceService MCP endpoint.");
                await Console.Error.WriteLineAsync("Make sure SpiceService is running, or provide the endpoint URL explicitly:");
                await Console.Error.WriteLineAsync("Usage: McpRemote.exe <http-endpoint-url>");
                await Console.Error.WriteLineAsync("Example: McpRemote.exe http://localhost:8081/mcp");
#if ENABLE_LOGGING
                await logger!.LogErrorAsync("Could not discover SpiceService MCP endpoint");
#endif
                return 1;
            }
            httpEndpoint = discoveredEndpoint;
            await Console.Error.WriteLineAsync($"McpRemote: Auto-discovered endpoint: {httpEndpoint}");
#if ENABLE_LOGGING
            logger!.LogInfo($"Auto-discovered endpoint: {httpEndpoint}");
#endif
        }
        else if (args.Length == 1)
        {
            httpEndpoint = args[0];
            
            // 2. Validate URL format
            if (!Uri.TryCreate(httpEndpoint, UriKind.Absolute, out var uri) || 
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                await Console.Error.WriteLineAsync($"Invalid HTTP endpoint: {httpEndpoint}");
#if ENABLE_LOGGING
                await logger!.LogErrorAsync($"Invalid HTTP endpoint: {httpEndpoint}");
#endif
                return 1;
            }
#if ENABLE_LOGGING
            logger!.LogInfo($"Using explicit endpoint: {httpEndpoint}");
#endif
        }
        else
        {
            await Console.Error.WriteLineAsync("Usage: McpRemote.exe [<http-endpoint-url>|auto|--discover]");
            await Console.Error.WriteLineAsync("Example: McpRemote.exe http://localhost:8081/mcp");
            await Console.Error.WriteLineAsync("Example: McpRemote.exe auto  (auto-discover endpoint)");
            return 1;
        }

        await Console.Error.WriteLineAsync($"McpRemote starting - proxying to {httpEndpoint}");
#if ENABLE_LOGGING
        logger!.LogInfo($"Starting proxy to {httpEndpoint}");
        await Console.Error.WriteLineAsync($"Log file: {logger.LogFilePath}");
#endif

        try
        {
            await RunProxyAsync(httpEndpoint, logger);
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Fatal error: {ex.Message}");
            await Console.Error.WriteLineAsync(ex.StackTrace ?? "");
#if ENABLE_LOGGING
            await logger!.LogErrorAsync($"Fatal error: {ex.Message}", ex);
#endif
            return 1;
        }
    }
    
    /// <summary>
    /// Auto-discover SpiceService MCP endpoint by trying common ports
    /// If multiple instances are found, selects the most recent one (highest process ID)
    /// </summary>
    static async Task<string?> DiscoverEndpointAsync(ConnectionLogger? logger = null)
    {
        var discoveredInstances = new List<(string endpoint, int processId, DateTime startTime)>();
        
        // Try ports 8081-8090 (common range for SpiceService)
        for (int port = 8081; port <= 8090; port++)
        {
            try
            {
                var discoveryUrl = $"http://127.0.0.1:{port}/discovery";
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromMilliseconds(500) // Quick timeout for discovery
                };
                
                var response = await httpClient.GetAsync(discoveryUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("mcpEndpoint", out var endpointElement))
                    {
                        var endpoint = endpointElement.GetString();
                        if (endpoint != null)
                        {
                            // Extract instance identification if available
                            int processId = 0;
                            DateTime startTime = DateTime.MinValue;
                            
                            if (doc.RootElement.TryGetProperty("processId", out var processIdElement))
                            {
                                processId = processIdElement.GetInt32();
                            }
                            
                            if (doc.RootElement.TryGetProperty("startTime", out var startTimeElement))
                            {
                                if (DateTime.TryParse(startTimeElement.GetString(), out var parsedTime))
                                {
                                    startTime = parsedTime;
                                }
                            }
                            
                            discoveredInstances.Add((endpoint, processId, startTime));
                        }
                    }
                }
            }
            catch
            {
                // Port not available or not SpiceService - try next
                continue;
            }
        }
        
        if (discoveredInstances.Count == 0)
        {
            return null;
        }
        
        // If multiple instances found, prefer the one with highest process ID (typically most recent)
        // Fallback to most recent start time if process IDs are equal
        var selected = discoveredInstances
            .OrderByDescending(i => i.processId)
            .ThenByDescending(i => i.startTime)
            .First();
        
        if (discoveredInstances.Count > 1)
        {
            await Console.Error.WriteLineAsync($"McpRemote: Found {discoveredInstances.Count} SpiceService instances, selected process {selected.processId} on port {new Uri(selected.endpoint).Port}");
#if ENABLE_LOGGING
            logger?.LogInfo($"Found {discoveredInstances.Count} SpiceService instances, selected process {selected.processId} on port {new Uri(selected.endpoint).Port}");
#endif
        }
        
        return selected.endpoint;
    }

    static async Task RunProxyAsync(string httpEndpoint, ConnectionLogger? logger)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Read from stdin, forward to HTTP, write response to stdout
        while (true)
        {
            // 1. Read JSON-RPC message from stdin
            var line = await Console.In.ReadLineAsync();
            
            // EOF - clean shutdown
            if (line == null)
            {
                await Console.Error.WriteLineAsync("McpRemote: stdin closed, shutting down");
#if ENABLE_LOGGING
                logger?.LogInfo("stdin closed, shutting down");
#endif
                break;
            }

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
                continue;

#if ENABLE_LOGGING
            // Log the incoming request
            await logger!.LogRequestAsync(line);
#endif

            // Parse request to check if it's a notification (no id) - do this before try block
            JsonElement? requestId = null;
            bool isNotification = false;
            try
            {
                var requestDoc = JsonDocument.Parse(line);
                if (requestDoc.RootElement.TryGetProperty("id", out var idElement))
                {
                    requestId = idElement.ValueKind == JsonValueKind.Null ? null : idElement;
                }
                else
                {
                    isNotification = true; // No id = notification
                }
            }
            catch
            {
                // Invalid JSON - will be handled by server, but we still need to track it
                requestId = null;
            }
            
            // JSON-RPC spec: Notifications don't get responses - skip HTTP request entirely
            if (isNotification)
            {
                // Still forward to server for logging/processing, but don't wait for or forward response
                // This prevents Claude Desktop from reading EOF from stdout
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var content = new StringContent(line, Encoding.UTF8, "application/json");
                        using var tempClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
#if ENABLE_LOGGING
                        await logger!.LogHttpRequestAsync(httpEndpoint, line);
#endif
                        var response = await tempClient.PostAsync(httpEndpoint, content);
                        var responseBody = await response.Content.ReadAsStringAsync();
#if ENABLE_LOGGING
                        await logger!.LogHttpResponseAsync((int)response.StatusCode, responseBody);
#endif
                        // Ignore response - notifications don't get responses
                    }
#if ENABLE_LOGGING
                    catch (Exception ex)
                    {
                        await logger!.LogErrorAsync("Error forwarding notification", ex);
                        // Silently ignore errors for notifications
                    }
#else
                    catch
                    {
                        // Silently ignore errors for notifications
                    }
#endif
                });
                continue; // Don't block waiting for response
            }
            
            try
            {
                // 2. Forward to HTTP endpoint
#if ENABLE_LOGGING
                await logger!.LogHttpRequestAsync(httpEndpoint, line);
#endif
                var content = new StringContent(line, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(httpEndpoint, content);

                // 3. Handle response based on status code
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
#if ENABLE_LOGGING
                    await logger!.LogHttpResponseAsync((int)response.StatusCode, responseBody);
#endif
                    
                    // 4. Write to stdout (MCP clients expect newline-delimited)
                    await Console.Out.WriteLineAsync(responseBody);
                    await Console.Out.FlushAsync();
                    
#if ENABLE_LOGGING
                    // Log the outgoing response
                    await logger!.LogResponseAsync(responseBody);
#endif
                }
                else
                {
                    // HTTP error - convert to JSON-RPC error response
                    var errorBody = await response.Content.ReadAsStringAsync();
#if ENABLE_LOGGING
                    await logger!.LogHttpResponseAsync((int)response.StatusCode, errorBody);
#endif
                    var errorMessage = $"HTTP {response.StatusCode}: {errorBody}";
                    
                    // Try to extract id from original request for error response
                    var errorResponse = new Dictionary<string, object?>
                    {
                        ["jsonrpc"] = "2.0",
                        ["error"] = new Dictionary<string, object>
                        {
                            ["code"] = (int)response.StatusCode == 400 ? -32600 : -32603,
                            ["message"] = errorMessage
                        }
                    };
                    
                    // Only include id if request had one (not a notification)
                    if (!isNotification && requestId.HasValue)
                    {
                        errorResponse["id"] = requestId.Value.ValueKind == JsonValueKind.Null 
                            ? null 
                            : (object)requestId.Value;
                    }
                    
                    var errorJson = JsonSerializer.Serialize(errorResponse);
                    await Console.Out.WriteLineAsync(errorJson);
                    await Console.Out.FlushAsync();
                    
#if ENABLE_LOGGING
                    // Log the error response
                    await logger!.LogResponseAsync(errorJson);
#endif
                }
            }
            catch (HttpRequestException ex)
            {
                await Console.Error.WriteLineAsync($"HTTP error: {ex.Message}");
#if ENABLE_LOGGING
                await logger!.LogErrorAsync($"HTTP error: {ex.Message}", ex);
#else
                _ = ex; // Suppress unused variable warning
#endif
                
                // Send error response back to MCP client (only if not a notification)
                if (!isNotification)
                {
                    var errorResponse = new Dictionary<string, object?>
                    {
                        ["jsonrpc"] = "2.0",
                        ["error"] = new Dictionary<string, object>
                        {
                            ["code"] = -32603,
                            ["message"] = $"HTTP request failed: {ex.Message}"
                        }
                    };
                    
                    // Include id if we have one
                    if (requestId.HasValue)
                    {
                        errorResponse["id"] = requestId.Value.ValueKind == JsonValueKind.Null 
                            ? null 
                            : (object)requestId.Value;
                    }
                    
                    var errorJson = JsonSerializer.Serialize(errorResponse);
                    await Console.Out.WriteLineAsync(errorJson);
                    await Console.Out.FlushAsync();
                    
#if ENABLE_LOGGING
                    // Log the error response
                    await logger!.LogResponseAsync(errorJson);
#endif
                }
            }
            catch (TaskCanceledException ex)
            {
                await Console.Error.WriteLineAsync("HTTP request timeout");
#if ENABLE_LOGGING
                await logger!.LogErrorAsync("HTTP request timeout", ex);
#else
                _ = ex; // Suppress unused variable warning
#endif
                
                // Send error response back to MCP client (only if not a notification)
                if (!isNotification)
                {
                    var errorResponse = new Dictionary<string, object?>
                    {
                        ["jsonrpc"] = "2.0",
                        ["error"] = new Dictionary<string, object>
                        {
                            ["code"] = -32603,
                            ["message"] = "HTTP request timeout"
                        }
                    };
                    
                    // Include id if we have one
                    if (requestId.HasValue)
                    {
                        errorResponse["id"] = requestId.Value.ValueKind == JsonValueKind.Null 
                            ? null 
                            : (object)requestId.Value;
                    }
                    
                    var errorJson = JsonSerializer.Serialize(errorResponse);
                    await Console.Out.WriteLineAsync(errorJson);
                    await Console.Out.FlushAsync();
                    
#if ENABLE_LOGGING
                    // Log the error response
                    await logger!.LogResponseAsync(errorJson);
#endif
                }
            }
        }
    }
}

