namespace SpiceSharp.Api.Tray.Services;

using SpiceSharp.Api.Tray.Models;

/// <summary>
/// Executes IDE configuration operations
/// </summary>
public static class ConfigurationExecutor
{
    /// <summary>
    /// Execute configuration for selected IDEs
    /// </summary>
    public static ConfigurationResult ExecuteConfiguration(
        List<DetectedIDE> selectedIDEs,
        string mcpEndpointUrl,
        string proxyExecutablePath,
        bool appendMode,
        bool createBackup)
    {
        var result = new ConfigurationResult
        {
            Timestamp = DateTime.Now,
            ConfiguredIDEs = new List<string>(),
            BackupPaths = new List<string>(),
            Errors = new List<string>(),
            VSCodeConfigJson = null // No longer used - VS Code is auto-configured
        };
        
        // Configure all selected IDEs (including VS Code - no longer requires manual setup)
        foreach (var ide in selectedIDEs.Where(i => i.IsSelected))
        {
            try
            {
                // 1. Create backup if requested and file exists
                if (createBackup)
                {
                    var backupPath = ConfigurationBackup.CreateBackup(ide.ConfigFilePath);
                    if (backupPath != null)
                    {
                        result.BackupPaths.Add($"{ide.Name}: {backupPath}");
                    }
                }
                
                // 2. Ensure directory exists (for Windsurf if executable detected but no config dir)
                var configDir = Path.GetDirectoryName(ide.ConfigFilePath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                // 3. Apply configuration
                // VS Code uses mcp.json with "mcpServers" format (supports stdio transport)
                // Use stdio transport with McpRemote.exe proxy (more reliable than WebSocket)
                if (ide.Name == "VS Code")
                {
                    // Use VS Code-specific methods that work with mcp.json format
                    if (appendMode)
                    {
                        ConfigurationMerger.AppendVSCodeConfiguration(ide.ConfigFilePath, mcpEndpointUrl, proxyExecutablePath);
                    }
                    else
                    {
                        ConfigurationMerger.OverwriteVSCodeConfiguration(ide.ConfigFilePath, mcpEndpointUrl, proxyExecutablePath);
                    }
                }
                else
                {
                    // Use standard methods for other IDEs
                    if (appendMode)
                    {
                        ConfigurationMerger.AppendConfiguration(ide.ConfigFilePath, mcpEndpointUrl, proxyExecutablePath);
                    }
                    else
                    {
                        ConfigurationMerger.OverwriteConfiguration(ide.ConfigFilePath, mcpEndpointUrl, proxyExecutablePath);
                    }
                }
                
                result.ConfiguredIDEs.Add(ide.Name);
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Errors.Add($"{ide.Name}: Permission denied. Try running as administrator or modify file permissions. ({ex.Message})");
            }
            catch (IOException ex) when (ex.Message.Contains("being used") || ex.Message.Contains("locked"))
            {
                result.Errors.Add($"{ide.Name}: File is locked. Please close {ide.Name} and try again.");
            }
            catch (DirectoryNotFoundException ex)
            {
                result.Errors.Add($"{ide.Name}: Directory not found. ({ex.Message})");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{ide.Name}: {ex.Message}");
            }
        }
        
        return result;
    }
}

