namespace SpiceSharp.Api.Tray.Services;

/// <summary>
/// Creates timestamped backups of configuration files
/// </summary>
public static class ConfigurationBackup
{
    /// <summary>
    /// Create timestamped backup of configuration file
    /// </summary>
    /// <param name="configFilePath">Path to the configuration file to backup</param>
    /// <returns>Path to backup file, or null if original doesn't exist</returns>
    public static string? CreateBackup(string configFilePath)
    {
        if (!File.Exists(configFilePath))
            return null;
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = $"{configFilePath}.backup_{timestamp}";
        
        // If backup already exists, increment counter
        var counter = 1;
        var finalBackupPath = backupPath;
        while (File.Exists(finalBackupPath))
        {
            finalBackupPath = $"{configFilePath}.backup_{timestamp}_{counter}";
            counter++;
        }
        
        File.Copy(configFilePath, finalBackupPath, overwrite: false);
        
        return finalBackupPath;
    }
}

