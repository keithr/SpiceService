# NetlistSvg.NET

[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**NetlistSvg.NET** is a .NET wrapper around the [netlistsvg](https://github.com/nturley/netlistsvg) JavaScript library, providing schematic diagram generation from JSON netlists and SpiceSharp circuits. It embeds the original JavaScript implementation and executes it via the Jint interpreter, ensuring identical output quality while providing a pure .NET deployment experience.

> **For AI Agents**: See `AI_DOCUMENTATION.md` in the package for comprehensive API documentation and usage patterns optimized for agentic AI consumption.

## Features

- ✅ **Single Assembly** - Reference one DLL; all dependencies embedded
- ✅ **Cross-Platform** - Runs on Windows, Linux, macOS via .NET 8+
- ✅ **Air-Gapped** - Zero network dependencies; all resources embedded
- ✅ **SpiceSharp Integration** - Direct rendering from SpiceSharp `Circuit` objects
- ✅ **Extensible Skins** - Built-in digital and analog skins, support for custom skins
- ✅ **Async Support** - Full async/await API with cancellation support
- ✅ **Concurrent Rendering** - Thread-safe rendering with configurable concurrency limits

## Installation

```bash
dotnet add package NetlistSvg
```

Or via Package Manager Console:

```powershell
Install-Package NetlistSvg
```

## Quick Start

### Rendering from JSON Netlist

```csharp
using NetlistSvg;

var netlistJson = @"{
    ""modules"": {
        ""top"": {
            ""cells"": {
                ""g1"": {
                    ""type"": ""$_AND_"",
                    ""port_directions"": { ""A"": ""input"", ""B"": ""input"", ""Y"": ""output"" },
                    ""connections"": { ""A"": [2], ""B"": [3], ""Y"": [4] }
                }
            },
            ""ports"": {
                ""a"": { ""direction"": ""input"", ""bits"": [2] },
                ""b"": { ""direction"": ""input"", ""bits"": [3] },
                ""y"": { ""direction"": ""output"", ""bits"": [4] }
            }
        }
    }
}";

using var renderer = new SchematicRenderer();
var svg = renderer.Render(netlistJson);

File.WriteAllText("output.svg", svg);
```

### Rendering from SpiceSharp Circuit

```csharp
using NetlistSvg;
using NetlistSvg.Skins;
using SpiceSharp;
using SpiceSharp.Components;

// Create a simple LED circuit
var circuit = new Circuit(
    new VoltageSource("V1", "vcc", "0", 9.0),
    new Resistor("R1", "vcc", "led_anode", 330),
    new Diode("D1", "led_anode", "0", "LED")
);

using var renderer = new SchematicRenderer();
var svg = renderer.Render(circuit, new SpiceRenderOptions 
{ 
    Skin = SkinType.Analog,
    ShowValues = true
});

File.WriteAllText("led-circuit.svg", svg);
```

### Using Different Skins

```csharp
using NetlistSvg;
using NetlistSvg.Skins;

using var renderer = new SchematicRenderer();

// Use digital skin (default)
var digitalSvg = renderer.Render(netlistJson, SkinType.Digital);

// Use analog skin
var analogSvg = renderer.Render(netlistJson, SkinType.Analog);

// Use custom skin
var customSkin = renderer.GetBuiltInSkin(SkinType.Digital);
// Modify customSkin as needed...
var customSvg = renderer.Render(netlistJson, customSkin);
```

### Async Rendering

```csharp
using var renderer = new SchematicRenderer();
using var cts = new CancellationTokenSource();

var svg = await renderer.RenderAsync(circuit, cancellationToken: cts.Token);
```

## API Reference

### SchematicRenderer

The main class for rendering netlists to SVG.

#### Methods

- `Render(string netlistJson)` - Renders a JSON netlist using the default skin
- `Render(string netlistJson, SkinType skinType)` - Renders with a specific built-in skin
- `Render(string netlistJson, string customSkinSvg)` - Renders with a custom skin
- `Render(Circuit circuit, SpiceRenderOptions? options = null)` - Renders a SpiceSharp circuit
- `RenderAsync(...)` - Async versions of all Render methods
- `ToNetlistJson(Circuit circuit, SpiceRenderOptions? options = null)` - Converts a SpiceSharp circuit to JSON
- `GetBuiltInSkin(SkinType skinType)` - Retrieves a built-in skin for modification
- `AvailableSkins` - Gets a list of available built-in skin types

#### Example: Custom Options

```csharp
var options = new RenderOptions
{
    MaxMemory = 1_000_000_000,  // 1GB memory limit
    Timeout = TimeSpan.FromMinutes(5),
    DefaultSkin = SkinType.Analog,
    MaxConcurrency = 8
};

using var renderer = new SchematicRenderer(options);
```

### SpiceRenderOptions

Options for rendering SpiceSharp circuits.

```csharp
var options = new SpiceRenderOptions
{
    Skin = SkinType.Analog,
    GroundNode = "0",              // Ground node name (default: "0")
    ShowValues = true,              // Display component values
    ExternalPorts = new[] { "in", "out" }  // Nodes to expose as ports
};
```

## Command Line Interface

The package includes a CLI tool for standalone usage:

```bash
# Basic usage
netlistsvg input.json -o output.svg

# Specify skin
netlistsvg input.json -o output.svg -s analog

# Print license information
netlistsvg --license
```

## Supported Components

### Digital Components
- Logic gates: AND, OR, NOT, NAND, NOR, XOR, XNOR
- Flip-flops, latches, multiplexers, and more

### Analog Components
- **Passive**: Resistors, Capacitors, Inductors
- **Semiconductors**: Diodes, LEDs, Transistors (NPN/PNP)
- **Sources**: Voltage sources, Current sources
- **Power**: VCC, GND symbols

See `component-symbols.md` for the complete list.

## Examples

### Simple LED Circuit

```csharp
var circuit = new Circuit(
    new VoltageSource("V1", "vcc", "0", 9.0),
    new Resistor("R1", "vcc", "led_anode", 330),
    new Diode("D1", "led_anode", "0", "LED")
);

using var renderer = new SchematicRenderer();
var svg = renderer.Render(circuit, new SpiceRenderOptions 
{ 
    Skin = SkinType.Analog,
    ExternalPorts = Array.Empty<string>()  // Closed circuit
});
```

### LED Flasher (Astable Multivibrator)

```csharp
var circuit = new Circuit(
    new VoltageSource("V1", "vcc", "0", 9.0),
    new Diode("L1", "vcc", "led1_cathode", "LED"),
    new Resistor("R1", "led1_cathode", "q1_c", 470),
    new Diode("L2", "vcc", "led2_cathode", "LED"),
    new Resistor("R4", "led2_cathode", "q2_c", 470),
    new Resistor("R2", "vcc", "q2_b", 47e3),
    new Resistor("R3", "vcc", "q1_b", 47e3),
    new Capacitor("C1", "q1_c", "q2_b", 10e-6),
    new Capacitor("C2", "q2_c", "q1_b", 10e-6),
    new BipolarJunctionTransistor("Q1", "q1_c", "q1_b", "0", "BC548", "NPN"),
    new BipolarJunctionTransistor("Q2", "q2_c", "q2_b", "0", "BC548", "NPN")
);

using var renderer = new SchematicRenderer();
var svg = renderer.Render(circuit, new SpiceRenderOptions 
{ 
    Skin = SkinType.Analog 
});
```

## Performance Considerations

- **Memory**: Default memory limit is 1GB, which handles most circuits. For very large circuits (10+ components with complex interconnections), increase to 2GB via `RenderOptions.MaxMemory`.
- **Concurrency**: Default max concurrency is 4. Adjust via `RenderOptions.MaxConcurrency`.
- **Timeout**: Default timeout is 60 seconds. Adjust via `RenderOptions.Timeout`.

For complex circuits, consider:
- Using async rendering to avoid blocking
- Increasing memory limits only if you encounter `MemoryLimitExceededException`
- Adjusting concurrency based on your workload

## Attribution & Licensing

This project wraps the following open-source projects:

| Project | License | URL |
|---------|---------|-----|
| netlistsvg | MIT | https://github.com/nturley/netlistsvg |
| ELK (elkjs) | EPL-2.0 | https://github.com/kieler/elkjs |
| Jint | BSD-2-Clause | https://github.com/sebastienros/jint |

Full license text is available via:

```csharp
var licenses = LicenseInfo.GetThirdPartyLicenses();
Console.WriteLine(licenses);
```

Or via CLI:

```bash
netlistsvg --license
```

## Requirements

- .NET 8.0 or later
- SpiceSharp 3.2.3+ (for SpiceSharp integration)

## Contributing

Contributions are welcome! Please ensure all tests pass before submitting a pull request.

```bash
dotnet test
```

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

## Acknowledgments

- **Neil Turley** - Original netlistsvg library
- **Kiel University** - ELK layout engine
- **Sebastien Ros** - Jint JavaScript interpreter

