SpiceService
============

Copyright (c) 2025 Keith Rule

This software is free for personal, non-commercial use. Commercial use requires a commercial license.

SpiceService is a comprehensive .NET 8 solution for SPICE-based circuit simulation, providing a clean API, MCP (Model Context Protocol) server, and Windows tray application for managing circuit simulation services. The project enables circuit design, analysis, and visualization through both programmatic APIs and MCP protocol integration.

## Quick Install

A pre-built MSI installer is available in the `dist` directory:
- **Location**: `dist\SpiceServiceTray.msi`
- **Installation**: Double-click the MSI file to install (per-user installation, no administrator privileges required)
- **Includes**: Tray application, MCP server, and 500+ SPICE component libraries

To build the installer yourself, see the [Installing via MSI](#installing-via-msi) section below.

Projects
--------
- `SpiceSharp.Api.Core`: Core models and services (AC/DC/transient analysis, netlist, components)
- `SpiceSharp.Api.Core.Tests`: Unit tests for the core library
- `SpiceSharp.Api.Plot`: Plotting library for visualizing circuit analysis results (line, Bode, bar, scatter plots)
- `SpiceSharp.Api.Plot.Tests`: Unit tests for the plotting library
- `SpiceSharp.Api.Web`: MCP (Model Context Protocol) server for circuit simulation
- `SpiceSharp.Api.Tray`: Windows Forms tray application for managing the MCP service
- `McpRemote`: Stdio ↔ HTTP proxy for IDE integration (enables IDEs to connect via stdio MCP protocol)
- `SpiceServiceTray.Installer`: WiX installer project for the tray application
- `SimpleLEDTest`: Sample console application demonstrating basic usage (not in solution)
- `TestWaveformApi`: Test application for waveform functionality (not in solution)

Prerequisites
-------------
- .NET SDK 8.0 or later
- Visual Studio 2022 (recommended) or VS Code with C# extension
- Windows (required for `SpiceSharp.Api.Tray` and installer projects)

NuGet Packages
--------------
- **SpiceSharp**: 3.2.3 (core simulation library)
- **SpiceSharpBehavioral**: 3.2.0 (behavioral sources with expressions)
- **OxyPlot.Core**: 2.1.2 (plotting library for chart generation)
- **OxyPlot.SkiaSharp**: 2.1.2 (PNG export support for plots)

Build
-----
```bash
# Build entire solution (Debug configuration)
dotnet build SpiceService.sln

# Build Release configuration
dotnet build SpiceService.sln --configuration Release

# Build specific project
dotnet build SpiceSharp.Api.Core/SpiceSharp.Api.Core.csproj
```

**Build Status**: ✅ Solution builds successfully in both Debug and Release configurations.

Test
----
```bash
# Run all tests
dotnet test SpiceService.sln

# Run tests for specific project
dotnet test SpiceSharp.Api.Core.Tests/SpiceSharp.Api.Core.Tests.csproj
```

Run MCP Server
--------------
```bash
# Start the MCP server
dotnet run --project SpiceSharp.Api.Web

# Specify custom port
dotnet run --project SpiceSharp.Api.Web -- --port=8080

# Disable discovery service
dotnet run --project SpiceSharp.Api.Web -- --no-discovery

# Custom discovery port
dotnet run --project SpiceSharp.Api.Web -- --discovery-port=9999
```

The MCP server will be available at `http://localhost:8081/mcp` (or the configured port). The server uses JSON-RPC 2.0 protocol and supports MCP tools for circuit simulation.

Run Tray Application
--------------------

The tray application (`SpiceSharp.Api.Tray`) is a Windows Forms system tray application that provides a user-friendly interface for managing the MCP server and circuit simulation services.

### Running from Source
```bash
dotnet run --project SpiceSharp.Api.Tray
```

### Installing via MSI

A Windows Installer (MSI) package is available for easy installation:

1. **Build the installer:**
   ```powershell
   cd SpiceServiceTray.Installer
   .\build-installer.ps1
   ```
   The MSI will be created at `dist\SpiceServiceTray.msi`

2. **Install:**
   - Right-click `SpiceServiceTray.msi` and select "Install"
   - Or double-click and follow the installation wizard
   - Installation is per-user (no administrator privileges required)

3. **Installation Location:**
   - Application: `%LocalAppData%\SpiceService\Tray\`
   - Libraries: `%LocalAppData%\SpiceService\Tray\libraries\`
   - Start Menu shortcuts are created automatically

### Tray Application Features

- **System Tray Integration**: Runs in the Windows system tray with a custom icon
- **MCP Server Management**: Automatically starts and manages the MCP server
- **Auto-Start on Login**: Optional automatic startup when Windows starts
- **Network Visibility Control**: Toggle between localhost-only and network-accessible modes
- **IDE Integration Configuration**: Dialog-based configuration for AI-powered IDEs (Claude Desktop, Cursor AI, VS Code, Windsurf)
- **Circuit Management**: View and manage circuits through a GUI dialog
- **Circuit Export**: Export circuits as SPICE netlists
- **Log Viewer**: Built-in log viewer for debugging and monitoring
- **Status Monitoring**: Real-time status display showing server state
- **Discovery Service**: Automatic UDP-based service discovery for MCP clients
- **Library Management**: Automatic discovery of SPICE component libraries from multiple locations

### Tray Application Context Menu

Right-click the tray icon to access:
- **Status**: Current server status (Running/Error)
- **Auto-start on Login**: Toggle automatic startup
- **Network Accessible**: Toggle network visibility (requires restart)
- **Configure IDE Integration...**: Configure AI-powered IDEs to connect to SpiceService
- **List Circuits...**: View and manage all circuits
- **Export Circuit...**: Export circuit as SPICE netlist
- **View Logs...**: Open log viewer window
- **About...**: Application information and version
- **Exit**: Close the application

### Library Path Configuration

The tray application automatically searches for SPICE component libraries (`.lib` files) in the following order:

1. **User Libraries** (highest priority): `Documents\SpiceService\libraries\`
2. **Installed Libraries**: Next to the executable in `libraries\` subdirectory
3. **Development Libraries**: Source directory `libraries\` (for development builds)
4. **Sample Libraries**: `sample_libraries\` directory (for testing)

The application includes 500+ SPICE library files from the KiCad Spice Library project, covering a wide range of components including:
- Semiconductors (diodes, BJTs, MOSFETs, JFETs)
- Passive components
- Integrated circuits
- And more

### Network Configuration

The tray application can operate in two modes:

- **Localhost Only** (default): MCP server is only accessible from the local machine
- **Network Accessible**: MCP server can be accessed from other devices on the network

Network visibility can be toggled from the tray menu, but requires an application restart to take effect. The setting is persisted in the Windows registry.

### Discovery Service

The tray application includes a UDP-based discovery service that broadcasts server availability on the local network. This allows MCP clients to automatically discover and connect to the server without manual configuration.

- **UDP Port**: 19847 (default)
- **Broadcast Interval**: 30 seconds
- **Protocol**: JSON-based announcement messages

See `mcp_discovery_spec.md` for detailed specification.

IDE Integration
---------------

SpiceService includes built-in support for configuring AI-powered IDEs to connect via the MCP (Model Context Protocol) protocol. The tray application provides a dialog-based configuration system that automatically detects installed IDEs and configures them to connect to SpiceService.

### Supported IDEs

- **Claude Desktop**: Automatic configuration via `%APPDATA%\Claude\mcp.json`
- **Cursor AI**: Automatic configuration via `%USERPROFILE%\.cursor\mcp.json`
- **VS Code**: Manual setup with copy-paste JSON configuration (workspace-level)
- **Windsurf**: Automatic configuration via `%USERPROFILE%\.codeium\windsurf\mcp.json`

### Configuration via Tray Application

1. **Open Configuration Dialog**: Right-click the tray icon → **Configure IDE Integration...**
2. **Select IDEs**: Check the IDEs you want to configure
3. **Choose Mode**: 
   - **Append** (recommended): Adds SpiceService to existing MCP configuration
   - **Overwrite**: Replaces entire MCP configuration file
4. **Backup Option**: Optionally create timestamped backups before modifying files
5. **Apply**: Click **Apply** to configure selected IDEs

### McpRemote.exe Proxy

SpiceService uses `McpRemote.exe`, a lightweight stdio ↔ HTTP proxy that enables IDEs using stdio-based MCP protocol to connect to SpiceService's HTTP-based MCP server. This eliminates the need for Node.js or other external dependencies.

**Features:**
- **Auto-Discovery**: Automatically finds the current SpiceService endpoint (ports 8081-8090)
- **Stdio Protocol**: Bridges stdio-based MCP clients to HTTP-based MCP servers
- **JSON-RPC Compliant**: Properly handles notifications and request/response patterns
- **Colocated**: Automatically copied to tray app output directory during build

**Usage:**
- **Auto-discovery mode**: `McpRemote.exe auto` or `McpRemote.exe --discover`
- **Explicit URL**: `McpRemote.exe http://localhost:8081/mcp`

The proxy is automatically configured by the IDE Integration dialog and is included in the MSI installer.

### Manual Configuration

If you prefer to configure IDEs manually, use the following configuration:

**Claude Desktop** (`%APPDATA%\Claude\mcp.json`):
```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Users\\YourName\\AppData\\Local\\SpiceService\\Tray\\McpRemote.exe",
      "args": ["auto"]
    }
  }
}
```

**Cursor AI** (`%USERPROFILE%\.cursor\mcp.json`):
```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Users\\YourName\\AppData\\Local\\SpiceService\\Tray\\McpRemote.exe",
      "args": ["auto"]
    }
  }
}
```

**VS Code** (workspace `.vscode/mcp.json`):
```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Users\\YourName\\AppData\\Local\\SpiceService\\Tray\\McpRemote.exe",
      "args": ["auto"]
    }
  }
}
```

**Windsurf** (`%USERPROFILE%\.codeium\windsurf\mcp.json`):
```json
{
  "mcpServers": {
    "spice-simulator": {
      "command": "C:\\Users\\YourName\\AppData\\Local\\SpiceService\\Tray\\McpRemote.exe",
      "args": ["auto"]
    }
  }
}
```

**Note**: Replace `C:\\Users\\YourName\\AppData\\Local\\SpiceService\\Tray\\McpRemote.exe` with the actual installation path. The IDE Integration dialog automatically uses the correct path.

### Development Builds

When running from source, `McpRemote.exe` is automatically copied to the tray app's output directory (`SpiceSharp.Api.Tray\bin\Debug\net8.0-windows\` or `Release\net8.0-windows\`) during build, ensuring it's always colocated with the tray app executable.

Run sample
----------
```bash
# Note: SimpleLEDTest is not part of the solution
dotnet run --project SimpleLEDTest/SimpleLEDTest.csproj
```

Features
--------

### Component Types
- Passive components: Resistor, Capacitor, Inductor
- Diodes (with models)
- Voltage and Current Sources (DC, AC, and Transient waveforms)
- Dependent Sources: VCVS, VCCS, CCVS, CCCS (voltage/current controlled voltage/current sources)
- **Behavioral Sources**: Voltage/Current sources with mathematical expressions
  - **Note**: Expressions must use literal numeric values, not parameter names (see `BEHAVIORAL_SOURCES.md` for details)
- Semiconductors: BJT (NPN/PNP), MOSFET (N/P), JFET (N/P)
- Switches: Voltage-controlled and current-controlled switches
- Mutual Inductance (Transformers)

### Analysis Types
- DC Operating Point
- DC Sweep
- Transient (Time-Domain) Analysis
- AC (Frequency-Domain) Analysis
- Parameter Sweeps
- Temperature Sweeps

### Plotting & Visualization
- **Line Plots**: For DC sweep and transient analysis results
- **Bode Plots**: Two-panel plots (magnitude and phase) for AC analysis
- **Bar Charts**: For operating point comparisons
- **Scatter Plots**: For general X-Y data visualization
- **Export Formats**: SVG (scalable vector graphics) and PNG (raster graphics)
- **Customization**: Titles, axis labels, logarithmic scales, custom colors, grid/legend options
- **MCP Tool**: `plot_results` tool available via MCP server for generating plots from analysis results

### Waveform Support
The MCP server supports time-domain waveforms for voltage and current sources, enabling dynamic circuit simulation.

#### Sine Waveform
Sine waveforms can be specified using either naming convention:

**Option 1: Explicit waveform type**
```json
{
  "name": "Vin",
  "componentType": "voltage_source",
  "nodes": ["input", "0"],
  "value": 0,
  "parameters": {
    "waveform": "sine",
    "amplitude": 0.5,
    "frequency": 1000.0,
    "offset": 0.0,
    "delay": 0.0,
    "damping": 0.0,
    "phase": 0.0
  }
}
```

**Option 2: Auto-detected from parameter names**
```json
{
  "name": "Vin",
  "componentType": "voltage_source",
  "nodes": ["input", "0"],
  "value": 0,
  "parameters": {
    "sine_amplitude": 0.5,
    "sine_frequency": 40.0
  }
}
```

**Parameters:**
- `amplitude` / `sine_amplitude` (required): Peak amplitude in volts or amperes
- `frequency` / `sine_frequency` (required): Frequency in Hz
- `offset` (optional, default: 0): DC offset
- `delay` (optional, default: 0): Time delay before waveform starts (seconds)
- `damping` (optional, default: 0): Damping factor (1/seconds)
- `phase` (optional, default: 0): Phase shift in degrees

**SPICE Netlist Export:**
Waveforms are automatically exported in SPICE format:
```
Vin input 0 SIN(0 0.5 40 0 0)
```

#### Pulse Waveform
Pulse waveforms generate periodic square pulses for digital circuit simulation.

```json
{
  "name": "Vpulse",
  "componentType": "voltage_source",
  "nodes": ["input", "0"],
  "value": 0,
  "parameters": {
    "waveform": "pulse",
    "v1": 0.0,
    "v2": 5.0,
    "td": 0.0,
    "tr": 1e-6,
    "tf": 1e-6,
    "pw": 1e-3,
    "per": 2e-3
  }
}
```

**Parameters:**
- `v1` (required): Initial voltage value
- `v2` (required): Pulsed voltage value
- `td` (required): Time delay before pulse starts (seconds)
- `tr` (required): Rise time (seconds)
- `tf` (required): Fall time (seconds)
- `pw` (required): Pulse width (seconds)
- `per` (required): Period (seconds)

**SPICE Netlist Export:**
```
Vpulse input 0 PULSE(0 5 0 1e-6 1e-6 1e-3 2e-3)
```

#### PWL (Piecewise Linear) Waveform
PWL waveforms allow custom voltage/current profiles defined by time-value pairs.

```json
{
  "name": "Vpwl",
  "componentType": "voltage_source",
  "nodes": ["input", "0"],
  "value": 0,
  "parameters": {
    "waveform": "pwl",
    "points": [
      [0.0, 0.0],
      [1e-3, 5.0],
      [2e-3, 0.0],
      [3e-3, -5.0],
      [4e-3, 0.0]
    ]
  }
}
```

**Parameters:**
- `points` (required): Array of `[time, voltage]` or `[time, current]` pairs
  - Each pair is an array of two numbers: `[time_in_seconds, value_in_volts_or_amps]`

**SPICE Netlist Export:**
```
Vpwl input 0 PWL(0 0 0.001 5 0.002 0 0.003 -5 0.004 0)
```

#### SFFM (Single-Frequency FM) Waveform
SFFM waveforms generate frequency-modulated signals for RF circuit simulation.

```json
{
  "name": "Vsffm",
  "componentType": "voltage_source",
  "nodes": ["input", "0"],
  "value": 0,
  "parameters": {
    "waveform": "sffm",
    "vo": 1.0,
    "va": 0.5,
    "fc": 1e6,
    "mdi": 0.1,
    "fs": 1e3
  }
}
```

**Parameters:**
- `vo` (required): DC offset voltage
- `va` (required): Amplitude of modulation
- `fc` (required): Carrier frequency (Hz)
- `mdi` (required): Modulation index
- `fs` (required): Signal frequency (Hz)

**Note:** Netlist export is supported. Simulation support pending SpiceSharp library update.

**SPICE Netlist Export:**
```
Vsffm input 0 SFFM(1 0.5 1e6 0.1 1e3)
```

#### AM (Amplitude Modulation) Waveform
AM waveforms generate amplitude-modulated signals for RF circuit simulation.

```json
{
  "name": "Vam",
  "componentType": "voltage_source",
  "nodes": ["input", "0"],
  "value": 0,
  "parameters": {
    "waveform": "am",
    "vo": 1.0,
    "va": 0.5,
    "mf": 1e3,
    "fc": 1e6
  }
}
```

**Parameters:**
- `vo` (required): DC offset voltage
- `va` (required): Amplitude of modulation
- `mf` (required): Modulation frequency (Hz)
- `fc` (required): Carrier frequency (Hz)

**Note:** Netlist export is supported. Simulation support pending SpiceSharp library update.

**SPICE Netlist Export:**
```
Vam input 0 AM(1 0.5 1e3 1e6)
```

MCP Server
-----------

The `SpiceSharp.Api.Web` project provides an MCP (Model Context Protocol) server for circuit simulation. The server uses JSON-RPC 2.0 protocol over HTTP and exposes tools for circuit management, component/model management, analysis operations, plotting, and library search.

### MCP Server Architecture

The MCP server is embedded within the tray application, providing an in-process HTTP server that handles MCP protocol requests. This eliminates the need for a separate web server process and simplifies deployment.

### MCP Endpoint

The MCP server exposes a single endpoint:
- **POST** `/mcp` - JSON-RPC 2.0 endpoint for all MCP operations

### Server Configuration

- **Default Port**: 8081 (automatically finds available port if in use)
- **Protocol**: JSON-RPC 2.0 over HTTP
- **CORS**: Enabled for cross-origin requests
- **Network Binding**: Configurable (localhost-only or network-accessible)

### Library Search

The MCP server includes a comprehensive library search service that can search through 500+ SPICE component library files:

- **Tool**: `search_libraries`
- **Search Capabilities**: Component name, manufacturer, description, parameters
- **Library Sources**: Automatically indexed from configured library paths
- **Response Format**: JSON with component details, models, and library file information

### Available MCP Tools

The server provides a comprehensive set of tools organized into categories:

#### Circuit Management
- `create_circuit` - Create a new circuit or switch to existing circuit
- `list_circuits` - List all circuits with details
- `get_circuit` - Get circuit details by ID
- `get_active_circuit` - Get currently active circuit
- `delete_circuit` - Delete a circuit

#### Component Management
- `add_component` - Add a component to a circuit
- `list_components` - List all components in a circuit
- `get_component` - Get a specific component by name
- `delete_component` - Remove a component from a circuit

#### Model Management
- `define_model` - Define a semiconductor model (diode, BJT, MOSFET, JFET)
- `list_models` - List all models in a circuit
- `get_model` - Get a specific model by name

#### Analysis Operations
- `run_operating_point` - Calculate DC operating point
- `run_dc_analysis` - Run DC sweep analysis
- `run_transient_analysis` - Run transient (time-domain) analysis
- `run_ac_analysis` - Run AC (frequency-domain) analysis
- `run_parameter_sweep` - Parameter sweep analysis
- `run_temperature_sweep` - Temperature sweep analysis
- `run_noise_analysis` - Noise analysis
- `run_impedance_analysis` - Impedance analysis
- `export_netlist` - Export circuit as SPICE netlist

#### Visualization
- `plot_results` - Generate plots from analysis results (line, Bode, bar, scatter)
- `render_schematic` - Render circuit as SVG schematic diagram (via NetlistSvg)

#### Library Search
- `search_libraries` - Search component libraries for parts
- `get_library_info` - Get information about library files

#### Service Management
- `get_service_status` - Get current service status and configuration

### MCP Tool Usage

All tools are accessed via JSON-RPC 2.0 requests to the `/mcp` endpoint:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "create_circuit",
    "arguments": {
      "circuit_id": "my_circuit",
      "description": "My test circuit",
      "make_active": true
    }
  }
}
```

### Plotting Results (`plot_results` MCP Tool)

The `plot_results` tool generates visualizations from circuit analysis results. It supports multiple plot types and export formats.

**Basic Usage:**
```json
{
  "circuit_id": "my_circuit",
  "signals": ["v(out)", "i(R1)"],
  "plot_type": "auto",
  "image_format": "png",
  "output_format": ["image"]
}
```

**Plot Types:**
- `auto`: Automatically selects based on analysis type (line for DC/transient, Bode for AC, bar for operating point)
- `line`: Standard line plot for time-domain or DC sweep data
- `bode`: Two-panel plot (magnitude in dB and phase in degrees) for AC analysis
- `bar`: Bar chart for operating point comparisons
- `scatter`: Scatter plot for custom X-Y relationships

**Export Formats:**
- `image`: Base64-encoded image (recommended for MCP clients)
- `text`: Raw SVG string (for SVG format only, useful when client can't display SVG images)
- `file`: Save to disk (note: filesystem isolation may prevent client access)

**Image Formats:**
- `png`: Raster graphics (recommended, displays in all MCP clients)
- `svg`: Scalable vector graphics (may not display in all clients, use `text` format as fallback)

**Customization Options:**
```json
{
  "options": {
    "title": "Custom Plot Title",
    "x_label": "Frequency (Hz)",
    "y_label": "Magnitude (dB)",
    "x_scale": "log",
    "y_scale": "linear",
    "grid": true,
    "legend": true,
    "colors": ["#FF0000", "#00FF00"],
    "width": 1200,
    "height": 800,
    "invert_signals": false
  }
}
```

**Notes:**
- Analysis results are cached automatically - run an analysis first, then plot
- Bode plots automatically scale magnitude axis to show negative dB values (important for filters)
- File saves may report success but files may not be accessible due to filesystem isolation - use `image` format and save manually
- SVG images may not display in all MCP clients - PNG is recommended

### Quick Start Example

```json
// 1. Create a circuit
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "create_circuit",
    "arguments": {
      "circuit_id": "led_test",
      "description": "LED IV curve"
    }
  }
}

// 2. Define a diode model
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "define_model",
    "arguments": {
      "circuit_id": "led_test",
      "model_name": "RED_LED",
      "model_type": "diode",
      "parameters": {
        "IS": 1e-15,
        "N": 3.5,
        "EG": 1.6,
        "RS": 0.5
      }
    }
  }
}

// 3. Add components
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "add_component",
    "arguments": {
      "circuit_id": "led_test",
      "name": "V1",
      "component_type": "voltage_source",
      "nodes": ["anode", "0"],
      "value": 0.0
    }
  }
}

{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "add_component",
    "arguments": {
      "circuit_id": "led_test",
      "name": "D1",
      "component_type": "diode",
      "nodes": ["anode", "0"],
      "model": "RED_LED"
    }
  }
}

// 4. Run DC sweep analysis
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "tools/call",
  "params": {
    "name": "run_dc_analysis",
    "arguments": {
      "circuit_id": "led_test",
      "source": "V1",
      "start": 0.0,
      "stop": 5.0,
      "step": 0.1,
      "exports": ["i(D1)", "v(anode)"]
    }
  }
}

// 5. Plot results
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "tools/call",
  "params": {
    "name": "plot_results",
    "arguments": {
      "circuit_id": "led_test",
      "signals": ["i(D1)"],
      "plot_type": "line",
      "image_format": "png",
      "output_format": ["image"]
    }
  }
}
```

### Component Type Naming

**Important**: Component types must be lowercase with underscores:
- `resistor`, `capacitor`, `inductor`
- `voltage_source`, `current_source`
- `diode`, `bjt_npn`, `bjt_pnp`
- `mosfet_n`, `mosfet_p`
- `jfet_n`, `jfet_p`
- `vcvs`, `vccs`, `ccvs`, `cccs`
- `behavioral_voltage_source`, `behavioral_current_source`
- `voltage_switch`, `current_switch`

### Model Type Naming

Model types must also be lowercase with underscores:
- `diode`
- `bjt_npn`, `bjt_pnp`
- `mosfet_n`, `mosfet_p`
- `jfet_n`, `jfet_p`
- `voltage_switch`, `current_switch`

### Behavioral Sources

Behavioral sources allow mathematical expressions for voltage or current:

```json
{
  "name": "V1",
  "component_type": "behavioral_voltage_source",
  "nodes": ["out", "0"],
  "parameters": {
    "expression": "V(input) * 2.5"
  }
}
```

**Important**: Expressions must use literal numeric values, not parameter names. For example:
- ✅ Correct: `"V(input) * 5.1"`
- ❌ Wrong: `"V(input) * {gain}"`

See `BEHAVIORAL_SOURCES.md` for detailed documentation.

### AC Analysis Requirements

For AC analysis, voltage/current sources must have AC parameters configured:

```json
{
  "name": "Vin",
  "component_type": "voltage_source",
  "nodes": ["input", "0"],
  "value": 0,
  "parameters": {
    "ac": 1.0,
    "acphase": 0.0
  }
}
```

Development
-----------

### Code Organization

- **Core Library**: `SpiceSharp.Api.Core/` - Core models and services
  - Services: `ComponentService`, `ACAnalysisService`, `DCAnalysisService`, `TransientAnalysisService`, etc.
  - Models: `Circuit`, `ComponentDefinition`, `ModelDefinition`, `AnalysisResults`
  
- **Plotting Library**: `SpiceSharp.Api.Plot/` - Plot generation and visualization
  - Plot types: Line, Bode, Bar, Scatter
  - Export formats: SVG, PNG
  - Uses OxyPlot for rendering
  
- **MCP Server**: `SpiceSharp.Api.Web/` - HTTP server and MCP protocol implementation
  - `MCPService.cs` - Core MCP tool execution
  - `DiscoveryService.cs` - UDP-based service discovery
  - `LibraryService.cs` - Component library indexing and search
  
- **Tray Application**: `SpiceSharp.Api.Tray/` - Windows Forms tray application
  - `TrayApplication.cs` - Main application logic and HTTP server hosting
  - `AboutDialog.cs` - About dialog with version info
  - `CircuitsDialog.cs` - Circuit management UI
  - `LogDialog.cs` - Log viewer
  - `ExportCircuitDialog.cs` - Circuit export UI
  - `IDEConfigurationDialog.cs` - IDE integration configuration dialog
  - `IDEConfigurationSuccessDialog.cs` - Configuration success feedback dialog
  - `Services/IDEDetector.cs` - IDE detection service
  - `Services/ConfigurationMerger.cs` - IDE configuration file management
  - `Services/ConfigurationExecutor.cs` - Configuration execution orchestrator
  - `Services/ConfigurationBackup.cs` - Configuration backup service
  
- **McpRemote**: `McpRemote/` - Stdio ↔ HTTP proxy for IDE integration
  - `Program.cs` - Main proxy implementation (stdio ↔ HTTP bidirectional proxy)
  - Auto-discovery support for finding SpiceService endpoint
  - JSON-RPC 2.0 compliant notification handling
  
- **Installer**: `SpiceServiceTray.Installer/` - WiX-based MSI installer
  - `Product.wxs` - Installer definition
  - `build-installer.ps1` - Build script
  - Includes icon integration and shortcut creation

### Project Structure
```
SpiceService/
├── SpiceSharp.Api.Core/          # Core library (models, services)
│   ├── Models/                   # Data models (Circuit, ComponentDefinition, etc.)
│   └── Services/                 # Analysis and component services
├── SpiceSharp.Api.Core.Tests/   # Unit tests for core library
├── SpiceSharp.Api.Plot/          # Plotting library (OxyPlot integration)
│   ├── PlotGenerator.cs          # Main plot generation logic
│   └── PlotOptions.cs            # Plot customization options
├── SpiceSharp.Api.Plot.Tests/   # Plotting library tests
├── SpiceSharp.Api.Web/           # MCP server (ASP.NET Core)
│   ├── Services/
│   │   ├── MCPService.cs         # MCP tool execution
│   │   ├── DiscoveryService.cs    # UDP discovery service
│   │   └── LibraryService.cs     # Library search service
│   └── Controllers/
│       └── MCPController.cs       # HTTP endpoint handler
├── SpiceSharp.Api.Tray/          # Windows Forms tray app
│   ├── TrayApplication.cs         # Main application logic
│   ├── AboutDialog.cs             # About dialog
│   ├── CircuitsDialog.cs         # Circuit management UI
│   ├── LogDialog.cs               # Log viewer
│   ├── IDEConfigurationDialog.cs   # IDE integration configuration
│   ├── IDEConfigurationSuccessDialog.cs  # Configuration feedback
│   └── Services/                  # Tray-specific services
│       ├── IDEDetector.cs         # IDE detection
│       ├── ConfigurationMerger.cs # Configuration file management
│       ├── ConfigurationExecutor.cs # Configuration orchestration
│       └── ConfigurationBackup.cs  # Backup service
├── McpRemote/                     # Stdio ↔ HTTP proxy
│   └── Program.cs                 # Proxy implementation
├── SpiceServiceTray.Installer/    # WiX installer
│   ├── Product.wxs                # Installer definition
│   ├── build-installer.ps1        # Build script
│   └── BUILD_INSTRUCTIONS.md      # Build instructions
├── Scripts/                       # Utility scripts
│   ├── CreateIcon.ps1             # Icon generation script
│   ├── IdentifyProblematicLibraries.ps1  # Library validation utility
│   └── VerifyBuild.ps1            # Build verification
├── resources/                      # Application resources
│   ├── spice.ico                  # Multi-resolution application icon
│   ├── spice_100x100.png          # Icon source images
│   └── spice_256x256.png
├── libraries/                     # SPICE component libraries (500+ files)
├── sample_libraries/              # Sample/test libraries
├── netlistsvg/                    # NetlistSvg integration for schematic rendering
└── dist/                          # Build output (MSI installer)
```

### Build Output
Binaries are generated in standard .NET output directories:
- `SpiceSharp.Api.Core/bin/Debug|Release/net8.0/`
- `SpiceSharp.Api.Web/bin/Debug|Release/net8.0/`
- `SpiceSharp.Api.Tray/bin/Debug|Release/net8.0/`

### Troubleshooting

**Build Errors:**
- Ensure all NuGet packages are restored: `dotnet restore`
- Verify .NET SDK version: `dotnet --version` (should be 8.0 or later)
- Clean and rebuild: `dotnet clean && dotnet build`

**Namespace Issues:**
- `SpiceSharpBehavioral` uses the root namespace `SpiceSharpBehavioral`, not `SpiceSharpBehavioral.Components`
- Behavioral sources (`BehavioralVoltageSource`, `BehavioralCurrentSource`) are in the root `SpiceSharpBehavioral` namespace

**Visual Studio:**
- If solution doesn't build in Visual Studio, try: `dotnet restore SpiceService.sln` from command line first
- Ensure NuGet package restore is enabled in Visual Studio settings

**MCP Server:**
- Ensure port is not already in use (default: 8081)
- Check firewall settings if connecting from remote clients
- Network visibility must be enabled in tray app for remote access
- Discovery service uses UDP port 19847 (ensure firewall allows)

**Tray Application:**
- Requires .NET 8.0 Desktop Runtime
- Single instance enforcement (only one instance can run)
- Check system tray for application icon if it doesn't appear
- View logs from tray menu if issues occur
- `McpRemote.exe` is automatically colocated with tray app during build (for development)

**IDE Integration:**
- Ensure SpiceService tray app is running before configuring IDEs
- Restart IDE after configuration to pick up changes
- VS Code requires workspace-level configuration (not global)
- `McpRemote.exe` uses auto-discovery by default - no need to hardcode URLs
- If auto-discovery fails, use explicit URL: `McpRemote.exe http://localhost:8081/mcp`

**Library Search:**
- Libraries are automatically indexed on startup
- Add custom libraries to `Documents\SpiceService\libraries\`
- Library indexing may take a few seconds on first startup
- Check logs for library indexing status

Additional Resources
--------------------

### Documentation Files

- **`mcp_discovery_spec.md`**: Complete specification for MCP server discovery protocol
- **`IDE-Integration-Configuration-Spec.md`**: Specification for IDE integration configuration feature
- **`McpRemote-Architecture-Summary.md`**: Architecture overview of the McpRemote stdio ↔ HTTP proxy
- **`SpiceServiceTray.Installer/BUILD_INSTRUCTIONS.md`**: Detailed MSI installer build instructions
- **`netlistsvg/AI_DOCUMENTATION.md`**: NetlistSvg integration documentation

### Icon Integration

The project includes a comprehensive icon system:

- **Icon Generation**: `Scripts/CreateIcon.ps1` converts PNG files to multi-resolution ICO format
- **Icon File**: `resources/spice.ico` contains sizes: 16x16, 32x32, 48x48, 64x64, 128x128, 256x256
- **Usage**: Icon is embedded in the executable and used throughout:
  - Application executable
  - Windows Forms dialogs
  - MSI installer
  - All shortcuts (Start Menu, Desktop, Startup)

### Component Libraries

The project includes 500+ SPICE component library files from the KiCad Spice Library project, covering:
- Semiconductors (diodes, BJTs, MOSFETs, JFETs)
- Passive components
- Integrated circuits
- Manufacturer-specific parts

Libraries are automatically discovered and indexed for search functionality.

### NetlistSvg Integration

The project includes NetlistSvg integration for rendering circuit schematics as SVG diagrams. This enables visual representation of circuits alongside simulation results.

License
-------

Copyright (c) 2025 Keith Rule

This software is free for personal, non-commercial use. Commercial use requires a commercial license.

**Third-Party Components:**
- **SpiceSharp**: Circuit simulation library (MIT/BSD-style license)
- **KiCad Spice Library**: Component libraries (GPL-3.0)
- **OxyPlot**: Plotting library
- **NetlistSvg**: Schematic rendering

See individual component licenses for details.

