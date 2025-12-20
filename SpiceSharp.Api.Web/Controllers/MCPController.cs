using Microsoft.AspNetCore.Mvc;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;

namespace SpiceSharp.Api.Web.Controllers;

/// <summary>
/// Controller for MCP (Modular Control Protocol) JSON-RPC 2.0 endpoint
/// </summary>
[ApiController]
[Route("mcp")]
public class MCPController : ControllerBase
{
    private readonly MCPService _mcpService;
    private readonly MCPServerConfig _config;

    public MCPController(MCPService mcpService, MCPServerConfig config)
    {
        _mcpService = mcpService;
        _config = config;
    }

    /// <summary>
    /// Handle JSON-RPC 2.0 requests
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleRequest([FromBody] JsonElement request)
    {
        try
        {
            // Validate JSON-RPC version
            if (!request.TryGetProperty("jsonrpc", out var jsonrpcElement) ||
                jsonrpcElement.GetString() != "2.0")
            {
                return BadRequest(CreateErrorResponse(null, -32600, "Invalid Request", "jsonrpc must be '2.0'"));
            }

            // Get method
            if (!request.TryGetProperty("method", out var methodElement))
            {
                return BadRequest(CreateErrorResponse(null, -32600, "Invalid Request", "method is required"));
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
                _ => throw new InvalidOperationException($"Method not found: {method}")
            };

            return Ok(CreateSuccessResponse(id, result));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Method not found"))
        {
            JsonElement? id = request.TryGetProperty("id", out var idElement) ? idElement : null;
            return BadRequest(CreateErrorResponse(id, -32601, "Method not found", ex.Message));
        }
        catch (ArgumentException ex)
        {
            JsonElement? id = request.TryGetProperty("id", out var idElement) ? idElement : null;
            // Build comprehensive error message - put ALL details in the message field
            // Some MCP clients don't properly display the data field, so we put everything in message
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" [Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}]";
            }
            // Also include structured data for clients that support it
            var errorData = new Dictionary<string, object>
            {
                { "message", errorMessage },
                { "type", "ArgumentException" },
                { "original_message", ex.Message }
            };
            if (ex.InnerException != null)
            {
                errorData["inner_exception"] = ex.InnerException.GetType().Name;
                errorData["inner_message"] = ex.InnerException.Message;
            }
            var errorResponse = CreateErrorResponse(id, -32602, errorMessage, JsonSerializer.Serialize(errorData));
            // Log the actual error response being sent for debugging
            System.Diagnostics.Debug.WriteLine($"[MCP Error] ArgumentException: {errorMessage}");
            System.Diagnostics.Debug.WriteLine($"[MCP Error] Response: {JsonSerializer.Serialize(errorResponse)}");
            // Use 200 OK with error in body (JSON-RPC spec allows this, some clients expect it)
            return Ok(errorResponse);
        }
        catch (NotImplementedException ex)
        {
            JsonElement? id = request.TryGetProperty("id", out var idElement) ? idElement : null;
            // For NotImplementedException, use the full message which should contain helpful information
            var errorMessage = ex.Message;
            var errorData = new Dictionary<string, object>
            {
                { "message", errorMessage },
                { "type", "NotImplementedException" },
                { "original_message", ex.Message },
                { "feature_not_available", true },
                { "helpful_message", errorMessage }
            };
            if (ex.InnerException != null)
            {
                errorData["inner_exception"] = ex.InnerException.GetType().Name;
                errorData["inner_message"] = ex.InnerException.Message;
            }
            // Use a specific error code for "not implemented" features
            var errorResponse = CreateErrorResponse(id, -32601, errorMessage, JsonSerializer.Serialize(errorData));
            System.Diagnostics.Debug.WriteLine($"[MCP Error] NotImplementedException: {errorMessage}");
            // Use 200 OK with error in body (JSON-RPC spec allows this)
            return Ok(errorResponse);
        }
        catch (InvalidOperationException ex)
        {
            JsonElement? id = request.TryGetProperty("id", out var idElement) ? idElement : null;
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" [Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}]";
            }
            var errorData = new Dictionary<string, object>
            {
                { "message", errorMessage },
                { "type", "InvalidOperationException" },
                { "original_message", ex.Message }
            };
            if (ex.InnerException != null)
            {
                errorData["inner_exception"] = ex.InnerException.GetType().Name;
                errorData["inner_message"] = ex.InnerException.Message;
            }
            var errorResponse = CreateErrorResponse(id, -32603, errorMessage, JsonSerializer.Serialize(errorData));
            System.Diagnostics.Debug.WriteLine($"[MCP Error] InvalidOperationException: {errorMessage}");
            // Use 200 OK with error in body (JSON-RPC spec allows this)
            return Ok(errorResponse);
        }
        catch (Exception ex)
        {
            JsonElement? id = request.TryGetProperty("id", out var idElement) ? idElement : null;
            // Include full exception details in message - make it VERY visible
            var errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $" [Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}]";
            }
            var errorData = new Dictionary<string, object>
            {
                { "message", errorMessage },
                { "type", ex.GetType().Name },
                { "original_message", ex.Message }
            };
            if (ex.InnerException != null)
            {
                errorData["inner_exception"] = ex.InnerException.GetType().Name;
                errorData["inner_message"] = ex.InnerException.Message;
            }
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                var stackLines = ex.StackTrace.Split('\n').Take(3);
                errorData["stack_trace"] = string.Join("; ", stackLines);
            }
            var errorResponse = CreateErrorResponse(id, -32603, errorMessage, JsonSerializer.Serialize(errorData));
            System.Diagnostics.Debug.WriteLine($"[MCP Error] Exception ({ex.GetType().Name}): {errorMessage}");
            // Use 200 OK with error in body (JSON-RPC spec allows this)
            return Ok(errorResponse);
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

    private object CreateSuccessResponse(JsonElement? id, object? result)
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

        return response;
    }

    private object CreateErrorResponse(JsonElement? id, int code, string message, string? data = null)
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

        return response;
    }
}

