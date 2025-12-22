# SpiceService

**Circuit Simulation Software with MCP Server Integration**

Copyright (c) 2025 Keith Rule

SpiceService is a comprehensive circuit simulation application that provides powerful SPICE-based circuit analysis through an easy-to-use Windows tray application and MCP (Model Context Protocol) server integration. Perfect for circuit designers, electronics enthusiasts, and AI-powered development workflows.

## Quick Install

A pre-built MSI installer is available in the `dist` directory:
- **Location**: `dist\SpiceServiceTray.msi`
- **Installation**: Double-click the MSI file to install (per-user installation, no administrator privileges required)
- **Includes**: Tray application, MCP server, and 500+ SPICE component libraries

## Features

### Circuit Simulation
- **DC Analysis**: Operating point and voltage/current sweeps
- **AC Analysis**: Frequency response (Bode plots) with magnitude and phase
- **Transient Analysis**: Time-domain simulation with customizable waveforms
- **Parameter Sweeps**: Analyze circuit behavior across component value ranges
- **Temperature Sweeps**: Evaluate circuit performance across temperature ranges
- **Noise Analysis**: Circuit noise characteristics
- **Impedance Analysis**: Frequency-dependent impedance measurements

### Component Library
- **500+ SPICE Libraries**: Comprehensive component database from KiCad Spice Library
- **Component Search**: Fast search across all library files
- **Subcircuit Support**: Reusable circuit blocks including speaker models
- **Speaker Database**: Thiele-Small parameter database for loudspeaker design
- **Model Definitions**: Support for diodes, BJTs, MOSFETs, JFETs, and more

### Speaker Design Tools
- **Speaker Search**: Find speakers by Thiele-Small parameters (FS, QTS, VAS, etc.)
- **Enclosure Design**: Calculate optimal sealed or vented box designs
- **Crossover Compatibility**: Check woofer/tweeter compatibility for crossover design
- **Speaker Subcircuits**: Pre-built SPICE models for real speaker drivers

### Visualization
- **Line Plots**: Time-domain and DC sweep visualization
- **Bode Plots**: Frequency response with magnitude (dB) and phase (degrees)
- **Bar Charts**: Operating point comparisons
- **Scatter Plots**: Custom X-Y data visualization
- **Schematic Rendering**: Visual circuit diagrams (SVG format)
- **Export Formats**: PNG and SVG output

### Waveform Support
- **Sine Waves**: AC signals with amplitude, frequency, phase, damping
- **Pulse Waves**: Digital square pulses for switching circuits
- **PWL (Piecewise Linear)**: Custom voltage/current profiles
- **SFFM/AM**: Frequency and amplitude modulation for RF circuits

### MCP Server Integration
- **JSON-RPC 2.0 Protocol**: Standard MCP protocol support
- **AI IDE Integration**: Works with Claude Desktop, Cursor AI, VS Code, Windsurf
- **Auto-Discovery**: Automatic server discovery on local network
- **Network Access**: Configurable localhost-only or network-accessible modes

### Tray Application
- **System Tray Integration**: Runs quietly in the background
- **Auto-Start**: Optional automatic startup on Windows login
- **Circuit Management**: View, create, and manage multiple circuits
- **Circuit Export**: Export circuits as SPICE netlists
- **Log Viewer**: Built-in debugging and monitoring
- **IDE Configuration**: One-click setup for AI-powered IDEs

## Installation

### Option 1: MSI Installer (Recommended)

1. Download `dist\SpiceServiceTray.msi`
2. Double-click to install (no administrator privileges required)
3. Application installs to: `%LocalAppData%\SpiceService\Tray\`
4. Start Menu shortcuts are created automatically

### Option 2: Build from Source

**Prerequisites:**
- .NET SDK 8.0 or later
- Windows (required for tray application)
- WiX Toolset v3.11+ (for building installer)

**Build Steps:**
```bash
# Build entire solution
dotnet build SpiceService.sln --configuration Release

# Build and run tray application
dotnet run --project SpiceSharp.Api.Tray/SpiceSharp.Api.Tray.csproj
```

**Build Installer:**
```powershell
cd SpiceServiceTray.Installer
.\build-installer.ps1
```

## Usage

### Starting the Application

After installation, SpiceService runs automatically in the system tray. Look for the SpiceService icon in your Windows system tray.

**Right-click the tray icon** to access:
- **Status**: View current server status
- **Auto-start on Login**: Enable/disable automatic startup
- **Network Accessible**: Toggle network visibility
- **Configure IDE Integration...**: Set up AI IDE connections
- **List Circuits...**: View and manage circuits
- **Export Circuit...**: Export as SPICE netlist
- **View Logs...**: Open log viewer
- **About...**: Application information
- **Exit**: Close application

### Using with AI IDEs

SpiceService integrates seamlessly with AI-powered development environments:

1. **Start SpiceService**: Ensure the tray application is running
2. **Configure IDE**: Right-click tray icon → **Configure IDE Integration...**
3. **Select IDEs**: Check the IDEs you want to configure
4. **Apply**: Click **Apply** to configure
5. **Restart IDE**: Restart your IDE to connect

**Supported IDEs:**
- Claude Desktop
- Cursor AI
- VS Code (with MCP extension)
- Windsurf

### Basic Circuit Simulation Workflow

#### 1. Create a Circuit
```json
{
  "method": "tools/call",
  "params": {
    "name": "create_circuit",
    "arguments": {
      "circuit_id": "my_circuit",
      "description": "My first circuit"
    }
  }
}
```

#### 2. Add Components
```json
{
  "method": "tools/call",
  "params": {
    "name": "add_component",
    "arguments": {
      "circuit_id": "my_circuit",
      "name": "V1",
      "component_type": "voltage_source",
      "nodes": ["input", "0"],
      "value": 5.0,
      "parameters": {"ac": 1}
    }
  }
}
```

#### 3. Run Analysis
```json
{
  "method": "tools/call",
  "params": {
    "name": "run_ac_analysis",
    "arguments": {
      "circuit_id": "my_circuit",
      "start_frequency": 20,
      "stop_frequency": 20000,
      "number_of_points": 100,
      "signals": ["v(input)"]
    }
  }
}
```

#### 4. Visualize Results
```json
{
  "method": "tools/call",
  "params": {
    "name": "plot_results",
    "arguments": {
      "circuit_id": "my_circuit",
      "signals": ["v(input)"],
      "plot_type": "bode",
      "image_format": "png"
    }
  }
}
```

### Working with Subcircuits

Subcircuits allow you to use pre-built circuit blocks, including speaker models:

#### 1. Search for Subcircuits
```json
{
  "method": "tools/call",
  "params": {
    "name": "library_search",
    "arguments": {
      "query": "275_030",
      "type": "subcircuit"
    }
  }
}
```

#### 2. Add Subcircuit to Circuit
```json
{
  "method": "tools/call",
  "params": {
    "name": "add_component",
    "arguments": {
      "circuit_id": "my_circuit",
      "name": "Xspeaker",
      "component_type": "subcircuit",
      "model": "275_030",
      "nodes": ["output", "0"]
    }
  }
}
```

#### 3. Import from Netlist
You can also import complete circuits with subcircuits from SPICE netlists:
```json
{
  "method": "tools/call",
  "params": {
    "name": "import_netlist",
    "arguments": {
      "circuit_name": "crossover",
      "netlist": "V1 input 0 AC 1\nX1 input 0 275_030\n.end"
    }
  }
}
```

### Speaker Design Workflow

#### 1. Search for Speakers
```json
{
  "method": "tools/call",
  "params": {
    "name": "search_speakers_by_parameters",
    "arguments": {
      "driver_type": ["tweeters"],
      "diameter_min": 0.75,
      "sensitivity_min": 88
    }
  }
}
```

#### 2. Design Enclosure
```json
{
  "method": "tools/call",
  "params": {
    "name": "calculate_enclosure_design",
    "arguments": {
      "model": "275_030",
      "enclosure_type": "sealed",
      "target_qtc": 0.707
    }
  }
}
```

#### 3. Check Crossover Compatibility
```json
{
  "method": "tools/call",
  "params": {
    "name": "check_crossover_compatibility",
    "arguments": {
      "woofer_model": "297_429",
      "tweeter_model": "275_030",
      "crossover_frequency": 2000,
      "crossover_order": 2
    }
  }
}
```

## Component Types

### Passive Components
- Resistors, Capacitors, Inductors
- Mutual Inductance (Transformers)

### Sources
- Voltage Sources (DC, AC, Transient waveforms)
- Current Sources
- Behavioral Sources (mathematical expressions)

### Semiconductors
- Diodes (with custom models)
- BJTs (NPN/PNP)
- MOSFETs (N-channel/P-channel)
- JFETs (N-channel/P-channel)
- Switches (voltage/current controlled)

### Dependent Sources
- VCVS, VCCS, CCVS, CCCS (voltage/current controlled sources)

### Subcircuits
- Reusable circuit blocks from library files
- Speaker models with Thiele-Small parameters
- Custom subcircuit definitions

## Library Management

### Library Locations

SpiceService automatically searches for SPICE library files (`.lib`) in this order:

1. **User Libraries** (highest priority): `Documents\SpiceService\libraries\`
2. **Installed Libraries**: `libraries\` subdirectory next to executable
3. **Development Libraries**: Source directory `libraries\` folder

### Adding Custom Libraries

1. Create `Documents\SpiceService\libraries\` directory
2. Copy your `.lib` files to this directory
3. Restart SpiceService (libraries are indexed on startup)

### Library Search

Search for components and subcircuits:
```json
{
  "method": "tools/call",
  "params": {
    "name": "library_search",
    "arguments": {
      "query": "2N2222",
      "limit": 10
    }
  }
}
```

## Network Configuration

### Localhost Only (Default)
- MCP server accessible only from local machine
- Most secure option
- Recommended for single-user setups

### Network Accessible
- MCP server accessible from other devices on network
- Enable from tray menu: **Network Accessible**
- Requires application restart
- Useful for remote IDE connections

### Discovery Service
- Automatic UDP-based service discovery
- Broadcasts server availability on local network
- Port: 19847 (default)
- Allows automatic client connection without manual configuration

## Troubleshooting

### Application Won't Start
- Check if another instance is already running (only one instance allowed)
- Verify .NET 8.0 Desktop Runtime is installed
- Check system tray for application icon
- View logs from tray menu: **View Logs...**

### MCP Server Not Accessible
- Ensure tray application is running
- Check firewall settings (port 8081 default)
- Verify network visibility setting in tray menu
- Check logs for connection errors

### Subcircuits Not Found
- Verify library files are in correct location
- Check that libraries are indexed (see logs)
- Use `reindex_libraries` tool to refresh index
- Ensure subcircuit name matches library definition

### IDE Integration Issues
- Ensure SpiceService is running before configuring IDE
- Restart IDE after configuration
- Check IDE-specific configuration requirements
- Verify `McpRemote.exe` is in installation directory

### Library Search Not Working
- Libraries are indexed on startup (may take a few seconds)
- Check logs for indexing status
- Verify library files are valid SPICE format
- Try `reindex_libraries` tool to refresh

## Project Structure

```
SpiceService/
├── SpiceSharp.Api.Core/          # Core simulation library
├── SpiceSharp.Api.Plot/          # Plotting and visualization
├── SpiceSharp.Api.Web/           # MCP server (HTTP/JSON-RPC)
├── SpiceSharp.Api.Tray/          # Windows tray application
├── McpRemote/                    # IDE integration proxy
├── SpiceServiceTray.Installer/   # MSI installer
├── libraries/                    # SPICE component libraries (500+ files)
└── dist/                         # Build output (MSI installer)
```

## Development

### Building from Source

```bash
# Build solution
dotnet build SpiceService.sln

# Run tests
dotnet test SpiceService.sln

# Run tray application
dotnet run --project SpiceSharp.Api.Tray

# Run MCP server standalone
dotnet run --project SpiceSharp.Api.Web
```

### Running Tests

```bash
# All tests
dotnet test SpiceService.sln

# Specific test project
dotnet test SpiceSharp.Api.Core.Tests
dotnet test SpiceSharp.Api.Web.Tests
```

## Additional Resources

- **MCP Discovery Spec**: `mcp_discovery_spec.md` - Service discovery protocol
- **IDE Integration**: `IDE-Integration-Configuration-Spec.md` - IDE setup details
- **Installer Build**: `SpiceServiceTray.Installer/BUILD_INSTRUCTIONS.md` - MSI build guide
- **Behavioral Sources**: `BEHAVIORAL_SOURCES.md` - Expression syntax guide

## License

Copyright (c) 2025 Keith Rule

This software is free for personal, non-commercial use. Commercial use requires a commercial license.

**Third-Party Components:**
- **SpiceSharp**: Circuit simulation library (MIT/BSD-style license)
- **KiCad Spice Library**: Component libraries (GPL-3.0)
- **OxyPlot**: Plotting library
- **NetlistSvg**: Schematic rendering

See individual component licenses for details.
