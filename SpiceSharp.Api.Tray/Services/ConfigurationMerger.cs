namespace SpiceSharp.Api.Tray.Services;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Merges or overwrites MCP configuration files
/// </summary>
public static class ConfigurationMerger
{
    /// <summary>
    /// Append/update SpiceService MCP configuration to existing config
    /// </summary>
    /// <param name="configFilePath">Path to the configuration file</param>
    /// <param name="mcpEndpointUrl">MCP endpoint URL to configure</param>
    /// <param name="proxyExecutablePath">Full path to McpRemote.exe proxy executable</param>
    public static void AppendConfiguration(string configFilePath, string mcpEndpointUrl, string proxyExecutablePath)
    {
        JsonObject config;
        
        // 1. Read existing config (or create new)
        if (File.Exists(configFilePath))
        {
            try
            {
                var json = File.ReadAllText(configFilePath);
                var parsed = JsonNode.Parse(json);
                config = parsed?.AsObject() ?? new JsonObject();
            }
            catch (JsonException)
            {
                // Invalid JSON - create new config
                config = new JsonObject();
            }
        }
        else
        {
            // Create directory if needed
            var dir = Path.GetDirectoryName(configFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            config = new JsonObject();
        }
        
        // 2. Ensure mcpServers object exists
        if (!config.ContainsKey("mcpServers"))
        {
            config["mcpServers"] = new JsonObject();
        }
        
        // 3. Add/update spice-simulator entry
        // Use "auto" for auto-discovery, or explicit URL if provided
        var mcpServers = config["mcpServers"]!.AsObject();
        mcpServers["spice-simulator"] = new JsonObject
        {
            ["command"] = proxyExecutablePath,
            ["args"] = new JsonArray("auto"), // Auto-discover endpoint instead of hardcoding URL
            ["description"] = "SPICE analog circuit simulator and analysis tools"
        };
        
        // 4. Write back with formatting
        var options = new JsonSerializerOptions { WriteIndented = true };
        var jsonText = JsonSerializer.Serialize(config, options);
        File.WriteAllText(configFilePath, jsonText);
    }
    
    /// <summary>
    /// Overwrite entire configuration file with SpiceService-only config
    /// </summary>
    /// <param name="configFilePath">Path to the configuration file</param>
    /// <param name="mcpEndpointUrl">MCP endpoint URL to configure</param>
    /// <param name="proxyExecutablePath">Full path to McpRemote.exe proxy executable</param>
    public static void OverwriteConfiguration(string configFilePath, string mcpEndpointUrl, string proxyExecutablePath)
    {
        // Create directory if needed
        var dir = Path.GetDirectoryName(configFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        var config = new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["spice-simulator"] = new JsonObject
                {
                    ["command"] = proxyExecutablePath,
                    ["args"] = new JsonArray(mcpEndpointUrl),
                    ["description"] = "SPICE analog circuit simulator and analysis tools"
                }
            }
        };
        
        var options = new JsonSerializerOptions { WriteIndented = true };
        var jsonText = JsonSerializer.Serialize(config, options);
        File.WriteAllText(configFilePath, jsonText);
    }
    
    /// <summary>
    /// Generate VS Code configuration JSON (for manual copy-paste)
    /// </summary>
    /// <param name="mcpEndpointUrl">MCP endpoint URL to configure</param>
    /// <param name="proxyExecutablePath">Full path to McpRemote.exe proxy executable</param>
    /// <returns>Formatted JSON string</returns>
    public static string GenerateVSCodeConfig(string mcpEndpointUrl, string proxyExecutablePath)
    {
        var config = new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["spice-simulator"] = new JsonObject
                {
                    ["command"] = proxyExecutablePath,
                    ["args"] = new JsonArray("auto"), // Auto-discover endpoint instead of hardcoding URL
                    ["description"] = "SPICE analog circuit simulator and analysis tools"
                }
            }
        };
        
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(config, options);
    }
}

