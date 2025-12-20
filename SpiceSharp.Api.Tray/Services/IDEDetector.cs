namespace SpiceSharp.Api.Tray.Services;

using SpiceSharp.Api.Tray.Models;

/// <summary>
/// Detects installed IDEs and their configuration paths
/// </summary>
public static class IDEDetector
{
    private static readonly Dictionary<string, Func<DetectedIDE>> IdeFactories = new()
    {
        ["Claude Desktop"] = () => DetectClaudeDesktop(),
        ["Cursor AI"] = () => DetectCursorAI(),
        ["VS Code"] = () => DetectVSCode(),
        ["Windsurf"] = () => DetectWindsurf()
    };
    
    /// <summary>
    /// Detect all installed IDEs
    /// </summary>
    public static List<DetectedIDE> DetectInstalledIDEs()
    {
        var detected = new List<DetectedIDE>();
        
        foreach (var factory in IdeFactories.Values)
        {
            var ide = factory();
            detected.Add(ide);
        }
        
        return detected;
    }
    
    private static DetectedIDE DetectClaudeDesktop()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "claude_desktop_config.json");
        
        var configDir = Path.GetDirectoryName(configPath);
        var isInstalled = Directory.Exists(configDir ?? string.Empty);
        
        return new DetectedIDE
        {
            Name = "Claude Desktop",
            ConfigFilePath = configPath,
            IsInstalled = isInstalled,
            IsSelected = isInstalled,
            RequiresManualSetup = false
        };
    }
    
    private static DetectedIDE DetectCursorAI()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cursor", "mcp.json");
        
        var configDir = Path.GetDirectoryName(configPath);
        var isInstalled = Directory.Exists(configDir ?? string.Empty);
        
        return new DetectedIDE
        {
            Name = "Cursor AI",
            ConfigFilePath = configPath,
            IsInstalled = isInstalled,
            IsSelected = isInstalled,
            RequiresManualSetup = false
        };
    }
    
    private static DetectedIDE DetectVSCode()
    {
        // Check common install locations
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe")
        };
        
        var isInstalled = paths.Any(File.Exists);
        
        // VS Code uses workspace-level configs, so we don't have a global config path
        // Return a placeholder path for display purposes
        return new DetectedIDE
        {
            Name = "VS Code",
            ConfigFilePath = string.Empty, // No global config path
            IsInstalled = isInstalled,
            IsSelected = false, // Never auto-select VS Code (requires manual setup)
            RequiresManualSetup = true
        };
    }
    
    private static DetectedIDE DetectWindsurf()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codeium", "windsurf", "mcp_config.json");
        
        // Multi-stage detection: config dir, executable locations
        var configDir = Path.GetDirectoryName(configPath);
        var hasConfigDir = Directory.Exists(configDir ?? string.Empty);
        
        if (hasConfigDir)
        {
            return new DetectedIDE
            {
                Name = "Windsurf",
                ConfigFilePath = configPath,
                IsInstalled = true,
                IsSelected = true,
                RequiresManualSetup = false
            };
        }
        
        // Check for executable (Windows)
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var windsurf = Path.Combine(programFiles, "Windsurf", "Windsurf.exe");
        if (File.Exists(windsurf))
        {
            return new DetectedIDE
            {
                Name = "Windsurf",
                ConfigFilePath = configPath,
                IsInstalled = true,
                IsSelected = true,
                RequiresManualSetup = false
            };
        }
        
        // Check LocalAppData (typical install location)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windsurfLocal = Path.Combine(localAppData, "Programs", "Windsurf", "Windsurf.exe");
        if (File.Exists(windsurfLocal))
        {
            return new DetectedIDE
            {
                Name = "Windsurf",
                ConfigFilePath = configPath,
                IsInstalled = true,
                IsSelected = true,
                RequiresManualSetup = false
            };
        }
        
        return new DetectedIDE
        {
            Name = "Windsurf",
            ConfigFilePath = configPath,
            IsInstalled = false,
            IsSelected = false,
            RequiresManualSetup = false
        };
    }
}

