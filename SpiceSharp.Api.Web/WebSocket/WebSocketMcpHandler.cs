using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;

namespace SpiceSharp.Api.Web.WebSocket;

/// <summary>
/// Handles WebSocket connections for MCP protocol
/// </summary>
public class WebSocketMcpHandler
{
    private readonly System.Net.WebSockets.WebSocket _webSocket;
    private readonly MCPService _mcpService;
    private readonly MCPServerConfig _config;

    public WebSocketMcpHandler(System.Net.WebSockets.WebSocket webSocket, MCPService mcpService, MCPServerConfig config)
    {
        _webSocket = webSocket;
        _mcpService = mcpService;
        _config = config;
    }

    public async Task HandleAsync()
    {
        var buffer = new byte[8192];
        
        while (_webSocket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None);
            
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(
                    System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
                break;
            }
            
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
            {
                var jsonRpc = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Process JSON-RPC request
                var response = await ProcessJsonRpcAsync(jsonRpc);
                
                if (response != null)
                {
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(responseBytes),
                        System.Net.WebSockets.WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
            }
        }
    }

    private async Task<string?> ProcessJsonRpcAsync(string jsonRpc)
    {
        try
        {
            var request = JsonSerializer.Deserialize<JsonElement>(jsonRpc);
            
            // Validate JSON-RPC version
            if (!request.TryGetProperty("jsonrpc", out var jsonrpcElement) ||
                jsonrpcElement.GetString() != "2.0")
            {
                return CreateErrorResponse(null, -32600, "Invalid Request", "jsonrpc must be '2.0'");
            }

            // Get method
            if (!request.TryGetProperty("method", out var methodElement))
            {
                return CreateErrorResponse(null, -32600, "Invalid Request", "method is required");
            }

            var method = methodElement.GetString() ?? throw new InvalidOperationException("method must be a string");
            JsonElement? id = request.TryGetProperty("id", out var idElement) ? idElement : null;
            var @params = request.TryGetProperty("params", out var paramsElement) ? paramsElement : default(JsonElement);

            // Route to appropriate handler
            object? result = method switch
            {
                "initialize" => HandleInitialize(),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolsCall(@params),
                "notifications/initialized" => null, // Notification - no response
                _ => throw new InvalidOperationException($"Method not found: {method}")
            };

            // Notifications don't get responses
            if (result == null && method == "notifications/initialized")
            {
                return null;
            }

            return CreateSuccessResponse(id, result);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Method not found"))
        {
            JsonElement? id = JsonSerializer.Deserialize<JsonElement>(jsonRpc).TryGetProperty("id", out var idElement) ? idElement : null;
            return CreateErrorResponse(id, -32601, "Method not found", ex.Message);
        }
        catch (Exception ex)
        {
            JsonElement? id = null;
            try
            {
                var request = JsonSerializer.Deserialize<JsonElement>(jsonRpc);
                id = request.TryGetProperty("id", out var idElement) ? idElement : null;
            }
            catch
            {
                // Ignore parse errors when getting ID
            }
            
            var errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            return CreateErrorResponse(id, -32603, errorMessage, ex.StackTrace);
        }
    }

    private object HandleInitialize()
    {
        return new
        {
            protocolVersion = _config.ProtocolVersion,
            serverInfo = new
            {
                name = _config.Name,
                version = _config.Version
            },
            capabilities = new
            {
                tools = new { }
            }
        };
    }

    private object HandleToolsList()
    {
        var tools = _mcpService.GetTools();
        return new
        {
            tools = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = t.InputSchema
            })
        };
    }

    private async Task<object> HandleToolsCall(JsonElement @params)
    {
        if (@params.ValueKind == JsonValueKind.Null || @params.ValueKind == JsonValueKind.Undefined)
            throw new ArgumentException("params is required for tools/call");

        if (!@params.TryGetProperty("name", out var nameElement))
            throw new ArgumentException("name is required in params");

        var toolName = nameElement.GetString() ?? throw new ArgumentException("name must be a string");
        var arguments = @params.TryGetProperty("arguments", out var argsElement) ? argsElement : default(JsonElement);

        var result = await _mcpService.ExecuteTool(toolName, arguments);
        return result;
    }

    private string CreateSuccessResponse(JsonElement? id, object? result)
    {
        var response = new Dictionary<string, object>
        {
            { "jsonrpc", "2.0" }
        };

        if (id.HasValue && id.Value.ValueKind != JsonValueKind.Null)
        {
            response["id"] = id.Value;
        }
        else if (id.HasValue)
        {
            response["id"] = null!;
        }

        if (result != null)
        {
            response["result"] = result;
        }

        return JsonSerializer.Serialize(response);
    }

    private string CreateErrorResponse(JsonElement? id, int code, string message, string? data = null)
    {
        var response = new Dictionary<string, object>
        {
            { "jsonrpc", "2.0" },
            { "error", new Dictionary<string, object>
                {
                    { "code", code },
                    { "message", message }
                }
            }
        };

        if (id.HasValue && id.Value.ValueKind != JsonValueKind.Null)
        {
            response["id"] = id.Value;
        }
        else
        {
            response["id"] = null!;
        }

        if (!string.IsNullOrEmpty(data))
        {
            ((Dictionary<string, object>)response["error"]!)["data"] = data;
        }

        return JsonSerializer.Serialize(response);
    }
}
