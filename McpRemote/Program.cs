using System.Text;
using System.Text.Json;

namespace McpRemote;

class Program
{
    static async Task<int> Main(string[] args)
    {
        string httpEndpoint;
        
        // 1. Parse arguments - support both explicit URL and auto-discovery
        if (args.Length == 0 || args[0] == "auto" || args[0] == "--discover")
        {
            // Auto-discovery mode: try to find SpiceService on common ports
            var discoveredEndpoint = await DiscoverEndpointAsync();
            if (discoveredEndpoint == null)
            {
                await Console.Error.WriteLineAsync("Error: Could not discover SpiceService MCP endpoint.");
                await Console.Error.WriteLineAsync("Make sure SpiceService is running, or provide the endpoint URL explicitly:");
                await Console.Error.WriteLineAsync("Usage: McpRemote.exe <http-endpoint-url>");
                await Console.Error.WriteLineAsync("Example: McpRemote.exe http://localhost:8081/mcp");
                return 1;
            }
            httpEndpoint = discoveredEndpoint;
            await Console.Error.WriteLineAsync($"McpRemote: Auto-discovered endpoint: {httpEndpoint}");
        }
        else if (args.Length == 1)
        {
            httpEndpoint = args[0];
            
            // 2. Validate URL format
            if (!Uri.TryCreate(httpEndpoint, UriKind.Absolute, out var uri) || 
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                await Console.Error.WriteLineAsync($"Invalid HTTP endpoint: {httpEndpoint}");
                return 1;
            }
        }
        else
        {
            await Console.Error.WriteLineAsync("Usage: McpRemote.exe [<http-endpoint-url>|auto|--discover]");
            await Console.Error.WriteLineAsync("Example: McpRemote.exe http://localhost:8081/mcp");
            await Console.Error.WriteLineAsync("Example: McpRemote.exe auto  (auto-discover endpoint)");
            return 1;
        }

        await Console.Error.WriteLineAsync($"McpRemote starting - proxying to {httpEndpoint}");

        try
        {
            await RunProxyAsync(httpEndpoint);
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Fatal error: {ex.Message}");
            await Console.Error.WriteLineAsync(ex.StackTrace ?? "");
            return 1;
        }
    }
    
    /// <summary>
    /// Auto-discover SpiceService MCP endpoint by trying common ports
    /// </summary>
    static async Task<string?> DiscoverEndpointAsync()
    {
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
                        return endpointElement.GetString();
                    }
                }
            }
            catch
            {
                // Port not available or not SpiceService - try next
                continue;
            }
        }
        
        return null;
    }

    static async Task RunProxyAsync(string httpEndpoint)
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
                break;
            }

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
                continue;

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
                        await tempClient.PostAsync(httpEndpoint, content);
                        // Ignore response - notifications don't get responses
                    }
                    catch
                    {
                        // Silently ignore errors for notifications
                    }
                });
                continue; // Don't block waiting for response
            }
            
            try
            {
                
                // 2. Forward to HTTP endpoint
                var content = new StringContent(line, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(httpEndpoint, content);

                // 3. Handle response based on status code
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    
                    // 4. Write to stdout (MCP clients expect newline-delimited)
                    await Console.Out.WriteLineAsync(responseBody);
                    await Console.Out.FlushAsync();
                }
                else
                {
                    // HTTP error - convert to JSON-RPC error response
                    var errorBody = await response.Content.ReadAsStringAsync();
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
                }
            }
            catch (HttpRequestException ex)
            {
                await Console.Error.WriteLineAsync($"HTTP error: {ex.Message}");
                
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
                }
            }
            catch (TaskCanceledException)
            {
                await Console.Error.WriteLineAsync("HTTP request timeout");
                
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
                }
            }
        }
    }
}

