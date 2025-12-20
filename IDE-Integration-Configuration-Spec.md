# IDE Integration Configuration Feature Specification

## Overview

This document specifies a dialog-based configuration system for the SpiceService tray application that enables frictionless integration with multiple AI-powered IDEs (Claude Desktop, Cursor AI, VS Code with Copilot, and Windsurf). The system automatically detects installed IDEs and configures their MCP (Model Context Protocol) settings to connect to the running SpiceService server.

## Business Context

**Target Users**: Fortune 500 engineering teams (AMD, NVidia, Texas Instruments, Lockheed Martin, etc.)

**Value Proposition**: Single-click configuration eliminates manual JSON editing, reducing integration friction from 15+ minutes to under 30 seconds.

**Success Criteria**: 
- 100% customer validation achieved through zero-configuration deployment
- Enterprise customers can deploy to engineering teams without IT support tickets
- Non-technical users can configure IDE integration without documentation

---

## Critical Architecture Decision: Node.js Independence

**Problem Identified**: Initial design assumed Node.js/npx availability for `mcp-remote` package. Testing revealed this dependency creates friction:
- Fortune 500 customers don't reliably have Node.js installed
- Requiring Node.js installation kills "frictionless" value proposition
- Log evidence: `'npx' is not recognized as an internal or external command`

**Solution**: Bundle `McpRemote.exe` - a lightweight .NET stdio↔HTTP proxy that ships with SpiceService.

### McpRemote.exe Component

A standalone .NET console application that bridges stdio (used by IDE MCP clients) to HTTP (used by SpiceService).

**Purpose**: Eliminates Node.js dependency while maintaining compatibility with all IDE MCP implementations.

**Location**: Installed alongside SpiceService executable
- Typical path: `C:\Program Files\SpiceService\McpRemote.exe`
- Portable install: `{InstallDir}\McpRemote.exe`

**Functionality**: Acts as transparent proxy between stdio and HTTP endpoints.

---

## Requirements from Parent Application

The parent SpiceService tray application **MUST** provide the following to the configuration dialog:

### Input Parameters

```csharp
public class IDEConfigurationInput
{
    /// <summary>
    /// The complete MCP endpoint URL (e.g., "http://localhost:8081/mcp")
    /// Parent app reads this from MCPServerConfig at runtime
    /// </summary>
    public string McpEndpointUrl { get; set; }
    
    /// <summary>
    /// Full path to McpRemote.exe proxy executable
    /// Typically in same directory as SpiceService.exe
    /// </summary>
    public string ProxyExecutablePath { get; set; }
    
    /// <summary>
    /// Optional: Server status to validate before configuration
    /// </summary>
    public bool IsServerRunning { get; set; }
}
```

### Example Usage

```csharp
// Parent app invokes dialog
var input = new IDEConfigurationInput
{
    McpEndpointUrl = _mcpConfig.GetEndpointUrl(), // e.g., "http://localhost:8081/mcp"
    ProxyExecutablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "McpRemote.exe"),
    IsServerRunning = _mcpService.IsHealthy()
};

var dialog = new IDEConfigurationDialog(input);
dialog.ShowDialog();
```

---

---

## McpRemote.exe Implementation Specification

### Overview

`McpRemote.exe` is a standalone .NET console application that acts as a transparent proxy between stdio (standard input/output) and HTTP endpoints. It enables IDE MCP clients to communicate with SpiceService's HTTP-based MCP server without requiring Node.js.

### Project Structure

```
SpiceService.sln
├── SpiceSharp.Api.Tray/          (existing)
├── SpiceSharp.Api.Web/           (existing)
├── SpiceSharp.Api.Core/          (existing)
└── McpRemote/                    ⭐ NEW
    ├── McpRemote.csproj
    ├── Program.cs
    └── Properties/
        └── launchSettings.json
```

### Project Configuration

**McpRemote.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>false</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```

**Key Settings**:
- `OutputType>Exe`: Console application
- `PublishSingleFile>true`: Single executable output
- `SelfContained>false`: Requires .NET runtime (already installed with SpiceService)
- `RuntimeIdentifier>win-x64`: Windows 64-bit target

### Implementation Requirements

#### Core Functionality

**Program.cs** must implement:

1. **Command-line argument parsing**:
   - Single required argument: HTTP endpoint URL
   - Example: `McpRemote.exe http://localhost:8081/mcp`

2. **Stdio ↔ HTTP bidirectional proxy**:
   - Read JSON-RPC messages from stdin (one per line)
   - Forward to HTTP endpoint via POST
   - Write HTTP responses to stdout (one per line)
   - Continue until stdin closes

3. **Error handling**:
   - Log errors to stderr (visible in IDE MCP logs)
   - Handle connection failures gracefully
   - Clean shutdown on stdin EOF

#### Protocol Details

**Input Format** (stdin):
- JSON-RPC 2.0 messages, newline-delimited
- Example: `{"jsonrpc":"2.0","method":"initialize","params":{...},"id":1}\n`

**Output Format** (stdout):
- JSON-RPC 2.0 responses, newline-delimited
- Example: `{"jsonrpc":"2.0","result":{...},"id":1}\n`

**HTTP Communication**:
- Method: POST
- Content-Type: `application/json`
- Body: Raw JSON-RPC message from stdin
- Response: JSON-RPC message written to stdout

### Reference Implementation

```csharp
using System.Text;

namespace McpRemote;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // 1. Validate arguments
        if (args.Length != 1)
        {
            await Console.Error.WriteLineAsync("Usage: McpRemote.exe <http-endpoint-url>");
            await Console.Error.WriteLineAsync("Example: McpRemote.exe http://localhost:8081/mcp");
            return 1;
        }

        var httpEndpoint = args[0];
        
        // 2. Validate URL format
        if (!Uri.TryCreate(httpEndpoint, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            await Console.Error.WriteLineAsync($"Invalid HTTP endpoint: {httpEndpoint}");
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

            try
            {
                // 2. Forward to HTTP endpoint
                var content = new StringContent(line, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(httpEndpoint, content);

                // 3. Read response
                var responseBody = await response.Content.ReadAsStringAsync();

                // 4. Write to stdout (MCP clients expect newline-delimited)
                await Console.Out.WriteLineAsync(responseBody);
                await Console.Out.FlushAsync();
            }
            catch (HttpRequestException ex)
            {
                await Console.Error.WriteLineAsync($"HTTP error: {ex.Message}");
                
                // Send error response back to MCP client
                var errorResponse = $$"""
                {
                    "jsonrpc": "2.0",
                    "error": {
                        "code": -32603,
                        "message": "HTTP request failed: {{ex.Message}}"
                    },
                    "id": null
                }
                """;
                
                await Console.Out.WriteLineAsync(errorResponse);
                await Console.Out.FlushAsync();
            }
            catch (TaskCanceledException)
            {
                await Console.Error.WriteLineAsync("HTTP request timeout");
                
                var errorResponse = """
                {
                    "jsonrpc": "2.0",
                    "error": {
                        "code": -32603,
                        "message": "HTTP request timeout"
                    },
                    "id": null
                }
                """;
                
                await Console.Out.WriteLineAsync(errorResponse);
                await Console.Out.FlushAsync();
            }
        }
    }
}
```

### Build and Deployment

#### Build Configuration

Add to SpiceService build process:

**Build Command**:
```bash
dotnet publish McpRemote/McpRemote.csproj -c Release -r win-x64 --self-contained false
```

**Output Location**:
```
McpRemote/bin/Release/net8.0/win-x64/publish/McpRemote.exe
```

#### Installation

Copy `McpRemote.exe` to SpiceService installation directory:
- Installer: Include in installation package
- Portable: Bundle in same directory as `SpiceService.exe`

**Expected Installation Path**:
```
C:\Program Files\SpiceService\
├── SpiceService.exe
├── McpRemote.exe          ⭐ Deployed here
├── SpiceSharp.Api.Web.dll
└── ... (other files)
```

### Testing McpRemote.exe

#### Manual Testing

**Test 1: Basic Communication**
```powershell
# Start SpiceService on port 8081
# Then test McpRemote manually:

echo '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}},"id":1}' | .\McpRemote.exe http://localhost:8081/mcp
```

Expected output: JSON-RPC response from SpiceService

**Test 2: Integration with Claude Desktop**
```json
// In claude_desktop_config.json
{
  "mcpServers": {
    "spice-simulator-test": {
      "command": "C:\\path\\to\\McpRemote.exe",
      "args": ["http://localhost:8081/mcp"]
    }
  }
}
```

Restart Claude Desktop, verify tools appear.

#### Automated Testing

Create unit tests in `McpRemote.Tests` project:

```csharp
[Fact]
public async Task Main_WithInvalidUrl_ReturnsErrorCode()
{
    var result = await Program.Main(new[] { "not-a-url" });
    Assert.Equal(1, result);
}

[Fact]
public async Task Main_WithNoArguments_ReturnsErrorCode()
{
    var result = await Program.Main(Array.Empty<string>());
    Assert.Equal(1, result);
}
```

### Error Scenarios

#### HTTP Server Not Running

**Input**: McpRemote starts but SpiceService is not running
**Behavior**: 
- Log error to stderr: "HTTP error: Connection refused"
- Send JSON-RPC error response to stdout
- Continue listening (server might start later)

#### Invalid JSON from stdin

**Input**: Malformed JSON on stdin
**Behavior**:
- Log warning to stderr
- Skip line, continue processing
- Do not crash

#### HTTP Timeout

**Input**: HTTP request takes >30 seconds
**Behavior**:
- Log timeout to stderr
- Send JSON-RPC timeout error to stdout
- Continue with next request

### Performance Considerations

**Latency**:
- Target: <10ms overhead per request
- Async I/O to prevent blocking
- No buffering (immediate flush to stdout)

**Memory**:
- Target: <20MB working set
- No message buffering
- Stream processing only

**Concurrency**:
- Single-threaded (stdio is sequential)
- One request at a time (MCP protocol is synchronous)

### Security Considerations

**Input Validation**:
- Validate HTTP endpoint URL format
- Prevent command injection (no shell execution)
- No file system access

**Network**:
- Only HTTP/HTTPS requests
- No arbitrary protocol support
- Configurable timeout

**Error Messages**:
- Don't leak sensitive information
- Generic error messages to stdout
- Detailed errors only to stderr

### Integration with Configuration Dialog

The configuration dialog will use `McpRemote.exe` as follows:

```csharp
// In ConfigurationMerger
public static void AppendConfiguration(
    string configFilePath,
    string mcpEndpointUrl,
    string proxyExecutablePath)  // Path to McpRemote.exe
{
    // ... JSON manipulation ...
    mcpServers["spice-simulator"] = new JObject
    {
        ["command"] = proxyExecutablePath,  // e.g., "C:\\Program Files\\SpiceService\\McpRemote.exe"
        ["args"] = new JArray(mcpEndpointUrl),
        ["description"] = "SPICE analog circuit simulator and analysis tools"
    };
}
```

### Troubleshooting Guide

**Problem**: IDE shows "spawn ENOENT" error
**Solution**: Verify `McpRemote.exe` exists at configured path

**Problem**: Tools don't appear in IDE
**Solution**: Check SpiceService is running, verify endpoint URL is correct

**Problem**: "Connection refused" in logs
**Solution**: Start SpiceService, ensure port is not blocked by firewall

**Problem**: Timeout errors
**Solution**: Check network connectivity, verify SpiceService is responding

---

## IDE Detection Logic

### Detection Strategy

The system SHALL auto-detect installed IDEs by checking for the existence of their configuration directories. Only detected IDEs are shown as enabled checkboxes in the UI.

### Windows Detection Paths

```csharp
public class IDEDetector
{
    private static readonly Dictionary<string, string> ConfigPaths = new()
    {
        ["Claude Desktop"] = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "claude_desktop_config.json"
        ),
        
        ["Cursor AI"] = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cursor", "mcp.json"
        ),
        
        ["VS Code"] = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".vscode", "mcp.json"
        ),
        
        ["Windsurf"] = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codeium", "windsurf", "mcp_config.json"
        )
    };
    
    /// <summary>
    /// Detect installed IDEs by checking if their config directories exist
    /// </summary>
    public static List<DetectedIDE> DetectInstalledIDEs()
    {
        var detected = new List<DetectedIDE>();
        
        foreach (var (ideName, configPath) in ConfigPaths)
        {
            var configDir = Path.GetDirectoryName(configPath);
            var isInstalled = Directory.Exists(configDir);
            
            detected.Add(new DetectedIDE
            {
                Name = ideName,
                ConfigFilePath = configPath,
                IsInstalled = isInstalled,
                IsSelected = isInstalled // Pre-select detected IDEs
            });
        }
        
        return detected;
    }
}
```

### Detection Rules

1. **Installed**: Configuration directory exists (e.g., `%APPDATA%\Claude`)
2. **Not Installed**: Configuration directory does not exist
3. **Default Selection**: All detected IDEs are pre-selected (checked)
4. **Not Detected**: IDE checkbox is shown but disabled/grayed out with "(not installed)" label

---

## Configuration File Formats

### MCP Configuration Structure

Each IDE uses a JSON configuration file with the following structure:

```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Program Files\\SpiceService\\McpRemote.exe",
      "args": ["http://localhost:8081/mcp"],
      "description": "SPICE analog circuit simulator and analysis tools"
    }
  }
}
```

**Key Points**:
- `command`: Full path to bundled McpRemote.exe proxy
- `args`: Single argument - the HTTP endpoint URL
- No Node.js/npx dependency required

### IDE-Specific Requirements

#### Claude Desktop
- **Path**: `%APPDATA%\Claude\claude_desktop_config.json`
- **Server Key**: `"spice-simulator"`
- **Required Fields**: `command`, `args`
- **Optional Fields**: `description`, `env`

#### Cursor AI
- **Path**: `%USERPROFILE%\.cursor\mcp.json`
- **Server Key**: `"spice-simulator"`
- **Required Fields**: `command`, `args`
- **Optional Fields**: `description`, `env`
- **Note**: Supports both `.cursor\mcp.json` (global) - use global only

#### VS Code (with Copilot)
- **Path**: `%USERPROFILE%\.vscode\mcp.json`
- **Server Key**: `"spice-simulator"`
- **Required Fields**: `command`, `args`
- **Optional Fields**: `description`
- **Note**: Requires VS Code 17.14+ and Agent Mode enabled

#### Windsurf
- **Path**: `%USERPROFILE%\.codeium\windsurf\mcp_config.json`
- **Server Key**: `"spice-simulator"`
- **Required Fields**: `command`, `args`
- **Optional Fields**: `description`, `env`

---

## Configuration Logic

### Append Mode (Default/Recommended)

**Behavior**: Intelligently merge SpiceService configuration into existing MCP servers.

```csharp
public class ConfigurationMerger
{
    /// <summary>
    /// Append/update SpiceService MCP configuration
    /// </summary>
    public static void AppendConfiguration(
        string configFilePath, 
        string mcpEndpointUrl,
        string proxyExecutablePath)
    {
        JObject config;
        
        // 1. Read existing config (or create new)
        if (File.Exists(configFilePath))
        {
            var json = File.ReadAllText(configFilePath);
            config = JObject.Parse(json);
        }
        else
        {
            // Create directory if needed
            var dir = Path.GetDirectoryName(configFilePath);
            Directory.CreateDirectory(dir);
            
            config = new JObject();
        }
        
        // 2. Ensure mcpServers object exists
        if (!config.ContainsKey("mcpServers"))
        {
            config["mcpServers"] = new JObject();
        }
        
        // 3. Add/update spice-simulator entry
        var mcpServers = (JObject)config["mcpServers"];
        mcpServers["spice-simulator"] = new JObject
        {
            ["command"] = proxyExecutablePath,
            ["args"] = new JArray(mcpEndpointUrl),
            ["description"] = "SPICE analog circuit simulator and analysis tools"
        };
        
        // 4. Write back with formatting
        File.WriteAllText(configFilePath, config.ToString(Formatting.Indented));
    }
}
```

**Rules**:
- If config file doesn't exist: Create it with SpiceService entry
- If `mcpServers` doesn't exist: Create it
- If `spice-simulator` already exists: Update it with new endpoint URL
- If other MCP servers exist: Preserve them unchanged
- Always write formatted JSON (2-space indent)

### Overwrite Mode

**Behavior**: Replace entire configuration file with SpiceService-only config.

```csharp
public static void OverwriteConfiguration(
    string configFilePath, 
    string mcpEndpointUrl,
    string proxyExecutablePath)
{
    // Create directory if needed
    var dir = Path.GetDirectoryName(configFilePath);
    Directory.CreateDirectory(dir);
    
    var config = new JObject
    {
        ["mcpServers"] = new JObject
        {
            ["spice-simulator"] = new JObject
            {
                ["command"] = proxyExecutablePath,
                ["args"] = new JArray(mcpEndpointUrl),
                ["description"] = "SPICE analog circuit simulator and analysis tools"
            }
        }
    };
    
    File.WriteAllText(configFilePath, config.ToString(Formatting.Indented));
}
```

**Rules**:
- Completely replace file contents
- Remove all other MCP server entries
- Useful for clean install or troubleshooting

---

## Backup Strategy

### Automatic Backups

Before **ANY** modification (append or overwrite), create timestamped backup:

```csharp
public class ConfigurationBackup
{
    /// <summary>
    /// Create timestamped backup of configuration file
    /// </summary>
    /// <returns>Path to backup file, or null if original doesn't exist</returns>
    public static string CreateBackup(string configFilePath)
    {
        if (!File.Exists(configFilePath))
            return null;
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = $"{configFilePath}.backup_{timestamp}";
        
        File.Copy(configFilePath, backupPath, overwrite: false);
        
        return backupPath;
    }
}
```

**Backup Rules**:
- Format: `{original_filename}.backup_YYYYMMDD_HHMMSS`
- Location: Same directory as original config file
- Always create backup if file exists (even for append mode)
- Never overwrite existing backups (increment if collision)
- Inform user of backup location in success message

**Example Backup Paths**:
```
claude_desktop_config.json.backup_20251219_143052
mcp.json.backup_20251219_143052
mcp_config.json.backup_20251219_143052
```

---

## User Interface Specification

### Dialog Layout

```
┌──────────────────────────────────────────────────────────┐
│ Configure IDE Integration                          [X]   │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Server Endpoint:                                        │
│  ┌────────────────────────────────────────────────────┐ │
│  │ http://localhost:8081/mcp                          │ │
│  └────────────────────────────────────────────────────┘ │
│  (Provided by SpiceService - read-only)                 │
│                                                          │
│  Proxy Executable:                                      │
│  ┌────────────────────────────────────────────────────┐ │
│  │ C:\Program Files\SpiceService\McpRemote.exe        │ │
│  └────────────────────────────────────────────────────┘ │
│  (Bundled with SpiceService - no Node.js required)      │
│                                                          │
│  Select IDEs to Configure:                              │
│  ┌────────────────────────────────────────────────────┐ │
│  │ ☑ Claude Desktop              (detected)           │ │
│  │ ☑ Cursor AI                   (detected)           │ │
│  │ ☐ Windsurf                    (not installed)      │ │
│  │ ☑ VS Code with Copilot        (detected)           │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  Configuration Mode:                                    │
│  ● Append to existing config (recommended)              │
│     Preserves other MCP servers, updates SpiceService   │
│                                                          │
│  ○ Overwrite entire config file                         │
│     Removes all other MCP servers (use with caution)    │
│                                                          │
│  ☑ Create backup before modifying files                 │
│                                                          │
│                           [Cancel]  [Apply Configuration]│
└──────────────────────────────────────────────────────────┘
```

### UI Component Specifications

#### Window Properties
- **Title**: "Configure IDE Integration"
- **Size**: 600px width × 560px height (increased to accommodate proxy display)
- **Resizable**: No
- **Modal**: Yes (blocks parent window)
- **Icon**: SpiceService application icon
- **Start Position**: CenterParent

#### Server Endpoint Display
- **Control**: Read-only TextBox
- **Value**: Provided by parent app via `IDEConfigurationInput.McpEndpointUrl`
- **Purpose**: Show user what endpoint will be configured
- **Label**: "Server Endpoint:"
- **Help Text**: "(Provided by SpiceService - read-only)"

#### Proxy Executable Display
- **Control**: Read-only TextBox
- **Value**: Provided by parent app via `IDEConfigurationInput.ProxyExecutablePath`
- **Purpose**: Show user the McpRemote.exe path that will be configured
- **Label**: "Proxy Executable:"
- **Help Text**: "(Bundled with SpiceService - no Node.js required)"
- **Validation**: On dialog load, verify file exists at specified path

#### IDE Selection List
- **Control**: CheckedListBox or ListView with checkboxes
- **Items**: Populated by `IDEDetector.DetectInstalledIDEs()`
- **Display Format**: `"[IDE Name]     (detected)"` or `"[IDE Name]     (not installed)"`
- **Default State**: All detected IDEs pre-checked
- **Disabled State**: Non-detected IDEs shown but grayed out and unchecked
- **Minimum Selection**: At least one IDE must be selected to enable Apply button

#### Configuration Mode
- **Control**: RadioButton group
- **Default**: "Append to existing config" selected
- **Options**:
  - **Append** (recommended): 
    - Label: "Append to existing config (recommended)"
    - Tooltip: "Preserves other MCP servers, updates SpiceService entry"
  - **Overwrite**: 
    - Label: "Overwrite entire config file"
    - Tooltip: "Removes all other MCP servers (use with caution)"

#### Backup Option
- **Control**: CheckBox
- **Default**: Checked (enabled)
- **Label**: "Create backup before modifying files"
- **Enabled**: Always (user can uncheck but not recommended)

#### Buttons

**Apply Configuration Button**:
- **Label**: "Apply Configuration"
- **Enabled**: Only when at least one IDE is selected
- **Action**: Execute configuration logic (see below)
- **Default Button**: Yes (Enter key triggers)

**Cancel Button**:
- **Label**: "Cancel"
- **Action**: Close dialog without changes
- **Cancel Button**: Yes (Escape key triggers)

---

## Configuration Execution Logic

### Apply Configuration Workflow

```csharp
public class ConfigurationExecutor
{
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
            Errors = new List<string>()
        };
        
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
                
                // 2. Apply configuration
                if (appendMode)
                {
                    ConfigurationMerger.AppendConfiguration(
                        ide.ConfigFilePath, 
                        mcpEndpointUrl, 
                        proxyExecutablePath);
                }
                else
                {
                    ConfigurationMerger.OverwriteConfiguration(
                        ide.ConfigFilePath, 
                        mcpEndpointUrl, 
                        proxyExecutablePath);
                }
                
                result.ConfiguredIDEs.Add(ide.Name);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{ide.Name}: {ex.Message}");
            }
        }
        
        return result;
    }
}
```

### Error Handling

#### File System Errors
- **Permission Denied**: Show error with suggestion to run as administrator
- **File Locked**: Show error suggesting closing the IDE first
- **Invalid JSON**: In append mode, fall back to overwrite with user confirmation
- **Disk Full**: Show error with disk space information

#### Validation Errors
- **No IDEs Selected**: Disable Apply button
- **Invalid Endpoint URL**: Should not occur (parent app provides it), but validate format
- **Server Not Running**: Warning (not blocking) - "SpiceService server may not be running"

---

## Success Feedback

### Success Dialog

After successful configuration, show modal success dialog:

```
┌──────────────────────────────────────────────────┐
│ Configuration Successful                    [X] │
├──────────────────────────────────────────────────┤
│                                                  │
│  ✓ Successfully configured the following IDEs:  │
│                                                  │
│    • Claude Desktop                              │
│    • Cursor AI                                   │
│    • VS Code with Copilot                        │
│                                                  │
│  Next Steps:                                     │
│  1. Restart configured IDEs to load new settings │
│  2. Look for SpiceService tools in MCP/Agent UI  │
│  3. Verify connection at http://localhost:5173   │
│                                                  │
│  Backups created at:                             │
│    Claude Desktop: ...\claude_desktop_config...  │
│    Cursor AI: ...\mcp.json.backup_20251219...   │
│    VS Code: ...\mcp.json.backup_20251219...     │
│                                                  │
│                                      [OK]        │
└──────────────────────────────────────────────────┘
```

### Success Message Components

1. **Header**: Clear success indicator (✓ icon + "Configuration Successful")
2. **Configured IDEs**: List of successfully configured tools
3. **Next Steps**: Actionable instructions for user
4. **Backup Paths**: Show where backups were saved (if created)
5. **OK Button**: Close success dialog

### Partial Success Handling

If some IDEs succeeded and others failed:

```
┌──────────────────────────────────────────────────┐
│ Configuration Partially Successful         [X] │
├──────────────────────────────────────────────────┤
│                                                  │
│  ✓ Successfully configured:                      │
│    • Claude Desktop                              │
│    • Cursor AI                                   │
│                                                  │
│  ✗ Configuration failed:                         │
│    • VS Code: Permission denied (run as admin)   │
│                                                  │
│  Next Steps:                                     │
│  1. Restart Claude Desktop and Cursor AI         │
│  2. Retry VS Code after closing the application  │
│                                                  │
│                                      [OK]        │
└──────────────────────────────────────────────────┘
```

---

## Confirmation Dialogs

### Overwrite Mode Confirmation

If user selects "Overwrite entire config file", show confirmation **before** execution:

```
┌──────────────────────────────────────────────────┐
│ ⚠ Confirm Overwrite                         [X] │
├──────────────────────────────────────────────────┤
│                                                  │
│  You are about to OVERWRITE the configuration    │
│  files for the following IDEs:                   │
│                                                  │
│    • Claude Desktop                              │
│    • Cursor AI                                   │
│                                                  │
│  This will REMOVE all other MCP server entries.  │
│                                                  │
│  ☑ Backup will be created before overwriting    │
│                                                  │
│  Continue with overwrite?                        │
│                                                  │
│                      [Cancel]  [Yes, Overwrite]  │
└──────────────────────────────────────────────────┘
```

**Rules**:
- Only shown when Overwrite mode is selected
- User can cancel and return to configuration dialog
- Backup checkbox state shown for confirmation
- Clear warning language (⚠ icon, "OVERWRITE", "REMOVE")

---

## IDE Restart Guidance

### Per-IDE Instructions

Provide specific restart instructions in success message:

**Claude Desktop**:
- "Quit Claude Desktop completely and restart"
- "Tools appear in chat - look for hammer icon 🔨"

**Cursor AI**:
- "Restart Cursor or reload window (Cmd/Ctrl + Shift + P → 'Reload Window')"
- "Tools auto-enable in Composer Agent"

**VS Code**:
- "Reload window (Cmd/Ctrl + Shift + P → 'Reload Window')"
- "Enable Agent Mode in Copilot Chat"
- "Select tools via 🛠️ icon in chat"

**Windsurf**:
- "Restart Windsurf or reload window"
- "Click Plugins icon in Cascade panel"
- "Click 'Refresh Servers' button"

---

## Implementation Checklist

### Phase 0: McpRemote.exe Proxy (PREREQUISITE)
- [ ] Create `McpRemote` console project
- [ ] Implement stdio ↔ HTTP proxy in `Program.cs`
- [ ] Add URL validation and error handling
- [ ] Test with SpiceService locally
- [ ] Verify integration with Claude Desktop
- [ ] Add to SpiceService build/deployment pipeline
- [ ] Update installer to include McpRemote.exe
- [ ] Document installation path for configuration dialog

**Estimated Time**: 2-3 hours  
**Blocking**: Configuration dialog cannot be tested without this

### Phase 1: Core Functionality
- [ ] Implement `IDEDetector` class with Windows path detection
- [ ] Implement `ConfigurationMerger` with append/overwrite logic
- [ ] Implement `ConfigurationBackup` with timestamped backups
- [ ] Implement `ConfigurationExecutor` with error handling
- [ ] Create main dialog UI matching specification
- [ ] Add validation logic (URL format, selected IDEs, proxy path)

### Phase 2: User Experience
- [ ] Implement success dialog with actionable next steps
- [ ] Implement overwrite confirmation dialog
- [ ] Implement partial success handling
- [ ] Add per-IDE restart guidance
- [ ] Add tooltips and help text
- [ ] Test with various existing config scenarios

### Phase 3: Edge Cases
- [ ] Handle missing directories (create them)
- [ ] Handle corrupted JSON (offer repair or overwrite)
- [ ] Handle permission errors (suggest elevation)
- [ ] Handle concurrent file access (retry logic)
- [ ] Handle invalid endpoint URLs (validation)
- [ ] Handle missing McpRemote.exe (critical error)
- [ ] Test with empty/minimal/complex existing configs

### Phase 4: Polish
- [ ] Add keyboard shortcuts (Enter = Apply, Esc = Cancel)
- [ ] Add progress indicator for multi-IDE configuration
- [ ] Add logging for troubleshooting
- [ ] Add "Copy to Clipboard" for VS Code instructions
- [ ] Test on various Windows versions (10, 11)
- [ ] Verify McpRemote.exe path resolution works in all install scenarios

---

## Testing Scenarios

### Happy Path Testing

1. **Clean Install**: No existing MCP configs
   - Expected: Create new config files with SpiceService entry
   
2. **Existing Configs**: Other MCP servers already configured
   - Append Mode: Preserve existing, add SpiceService
   - Overwrite Mode: Remove existing, only SpiceService remains

3. **Update Existing**: SpiceService already configured, different endpoint
   - Append Mode: Update endpoint URL
   - Overwrite Mode: Replace entire config with new endpoint

### Error Scenario Testing

1. **Permission Denied**: Config file is read-only
   - Expected: Error message with admin suggestion
   
2. **IDE Running**: File locked by running application
   - Expected: Error message suggesting closing IDE
   
3. **Corrupted JSON**: Invalid JSON in existing config
   - Append Mode: Offer to overwrite
   - Overwrite Mode: Proceed normally

4. **Disk Full**: Insufficient disk space
   - Expected: Clear error message about disk space

5. **McpRemote.exe Missing**: Proxy executable not found
   - Expected: Critical error dialog, cannot proceed
   - Message: "McpRemote.exe not found. Please reinstall SpiceService."
   
6. **McpRemote.exe Not Executable**: File exists but cannot be executed
   - Expected: Error message with permission/integrity check suggestion

### Edge Case Testing

1. **No IDEs Installed**: All checkboxes disabled
   - Expected: Informative message, Apply button disabled
   
2. **Partial Selection**: Some IDEs selected, some not
   - Expected: Only selected IDEs configured
   
3. **Backup Disabled**: User unchecks backup option
   - Expected: No backup created, proceed with config
   
4. **Rapid Re-configuration**: User runs config multiple times
   - Expected: Multiple backups created with different timestamps

---

## Success Metrics

### User Experience Metrics
- **Time to Configure**: < 30 seconds from tray menu to success
- **Clicks Required**: ≤ 3 clicks (menu → select IDEs → apply)
- **Error Rate**: < 5% of configuration attempts
- **Support Tickets**: Zero increase from configuration issues

### Technical Metrics
- **Config File Corruption**: 0% (always create backups)
- **False Negatives**: < 1% (IDE installed but not detected)
- **False Positives**: 0% (IDE not installed but shown as detected)
- **Backup Success**: 100% (backup created before every modification)

---

## Future Enhancements (Out of Scope)

### Potential V2 Features
- macOS and Linux support
- Project-level configuration options (vs global only)
- Configuration validation (test connection to server)
- One-click "Undo Configuration" button
- Export/import configuration profiles
- Remote configuration for team deployments
- Telemetry on which IDEs customers use most

---

## Technical Notes for Implementer

### JSON Library
Use `Newtonsoft.Json` (Json.NET) for robust JSON handling:
```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
```

### File System Safety
Always use try-catch and proper disposal:
```csharp
try
{
    using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
    using (var writer = new StreamWriter(fileStream))
    {
        writer.Write(json);
    }
}
catch (IOException ex)
{
    // Handle locked file
}
```

### Thread Safety
Configuration should run on background thread with UI updates via `Invoke`:
```csharp
await Task.Run(() => ExecuteConfiguration(...));
```

### Logging
Log all configuration actions for troubleshooting:
```csharp
Logger.Info($"Configuring {ide.Name} at {ide.ConfigFilePath}");
Logger.Info($"Backup created at {backupPath}");
Logger.Error($"Failed to configure {ide.Name}: {ex.Message}");
```

---

## Appendix A: Example Configuration Files

### Claude Desktop - Fresh Install
```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Program Files\\SpiceService\\McpRemote.exe",
      "args": ["http://localhost:8081/mcp"],
      "description": "SPICE analog circuit simulator and analysis tools"
    }
  }
}
```

### Cursor AI - With Existing Server (Append Mode)
```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_xxxx"
      }
    },
    "spice-simulator": {
      "command": "C:\\Program Files\\SpiceService\\McpRemote.exe",
      "args": ["http://localhost:8081/mcp"],
      "description": "SPICE analog circuit simulator and analysis tools"
    }
  }
}
```

### VS Code - Overwrite Mode
```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Program Files\\SpiceService\\McpRemote.exe",
      "args": ["http://localhost:8081/mcp"],
      "description": "SPICE analog circuit simulator and analysis tools"
    }
  }
}
```

---

## Appendix B: Error Messages Reference

### File System Errors

| Error | Message | Suggested Action |
|-------|---------|------------------|
| Permission Denied | "Unable to write configuration file. Permission denied." | "Try running SpiceService as administrator, or modify file permissions." |
| File Locked | "Configuration file is locked by another process." | "Close [IDE Name] and try again." |
| Directory Not Found | "Configuration directory does not exist." | "Will be created automatically." |
| Disk Full | "Insufficient disk space to write configuration." | "Free up disk space and try again." |

### JSON Errors

| Error | Message | Suggested Action |
|-------|---------|------------------|
| Invalid JSON | "Existing configuration file contains invalid JSON." | "Would you like to overwrite the corrupted file?" |
| Parse Error | "Unable to parse configuration file." | "File may be corrupted. Overwrite recommended." |

### Validation Errors

| Error | Message | Suggested Action |
|-------|---------|------------------|
| No IDEs Selected | "Please select at least one IDE to configure." | "Check at least one IDE checkbox." |
| Invalid URL | "Server endpoint URL is invalid." | "Contact support - this should not occur." |
| McpRemote.exe Not Found | "McpRemote.exe not found at expected location." | "Reinstall SpiceService or contact support." |
| McpRemote.exe Access Denied | "Cannot access McpRemote.exe. Permission denied." | "Check file permissions or run as administrator." |

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-19 | Specification | Initial specification for implementation |
| 1.1 | 2025-12-19 | Specification | **CRITICAL UPDATE**: Replaced npx/Node.js dependency with bundled McpRemote.exe proxy. Added complete implementation spec for stdio↔HTTP bridge. Updated all configuration examples and logic. |

---

## Sign-Off

**Specification Author**: AI Agent  
**Review Required By**: Keith Rule (Principal Engineer)  
**Implementation Target**: SpiceService Tray Application + McpRemote.exe Proxy  
**Expected Implementation Time**: 
- McpRemote.exe: 2-3 hours (prerequisite)
- Configuration Dialog: 4-6 hours  
- **Total: 6-9 hours**  
**Priority**: High - Critical for frictionless Fortune 500 deployment  
**Blocking Dependencies**: McpRemote.exe must be implemented and tested before configuration dialog
