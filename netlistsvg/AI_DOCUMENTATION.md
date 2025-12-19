# NetlistSvg.NET - AI Agent Documentation

## Overview

**NetlistSvg.NET** is a .NET library that generates SVG schematic diagrams from electronic circuit netlists. It wraps the JavaScript `netlistsvg` library and executes it via the Jint interpreter, providing a pure .NET deployment experience.

**Primary Use Case**: Convert SpiceSharp `Circuit` objects or JSON netlists into SVG schematic diagrams.

## Key Capabilities

1. **SpiceSharp Integration**: Direct rendering from `SpiceSharp.Circuit` objects
2. **JSON Netlist Support**: Render from standard netlist JSON format
3. **Multiple Skins**: Digital and analog schematic skins
4. **Async Support**: Full async/await API with cancellation
5. **Concurrent Rendering**: Thread-safe rendering with configurable limits

## Core API

### SchematicRenderer Class

The main entry point for rendering netlists to SVG.

**Constructor:**
```csharp
SchematicRenderer()  // Uses default options (1GB memory, 60s timeout)
SchematicRenderer(RenderOptions options)  // Custom configuration
```

**Key Methods:**

#### Render from SpiceSharp Circuit
```csharp
string Render(Circuit circuit, SpiceRenderOptions? options = null)
Task<string> RenderAsync(Circuit circuit, SpiceRenderOptions? options = null, CancellationToken cancellationToken = default)
```

#### Render from JSON Netlist
```csharp
string Render(string netlistJson)
string Render(string netlistJson, SkinType skinType)
string Render(string netlistJson, string customSkinSvg)
Task<string> RenderAsync(string netlistJson, CancellationToken cancellationToken = default)
Task<string> RenderAsync(string netlistJson, SkinType skinType, CancellationToken cancellationToken = default)
```

#### Utility Methods
```csharp
string ToNetlistJson(Circuit circuit, SpiceRenderOptions? options = null)  // Convert circuit to JSON
string GetBuiltInSkin(SkinType skinType)  // Get skin for modification
IReadOnlyList<SkinType> AvailableSkins { get; }  // List available skins
```

**Disposal:** Implements `IDisposable` - always dispose when done:
```csharp
using var renderer = new SchematicRenderer();
// ... use renderer ...
```

### RenderOptions Class

Configuration for `SchematicRenderer`.

**Properties:**
- `MaxMemory` (long): Maximum memory in bytes. Default: 1,000,000,000 (1GB)
- `Timeout` (TimeSpan): Rendering timeout. Default: 60 seconds
- `DefaultSkin` (SkinType): Default skin when not specified. Default: `SkinType.Digital`
- `MaxInputLength` (int): Maximum input JSON size. Default: 10MB
- `MaxConcurrency` (int): Maximum concurrent renders. Default: 4

**When to Increase MaxMemory:**
- Default 1GB handles most circuits (up to ~10 components)
- Increase to 2GB for very large circuits (20+ components)
- Only increase if you encounter `MemoryLimitExceededException`

### SpiceRenderOptions Class

Options for rendering SpiceSharp circuits.

**Properties:**
- `Skin` (SkinType): Skin to use. Default: `SkinType.Analog`
- `GroundNode` (string): Ground node name. Default: "0"
- `ShowValues` (bool): Display component values. Default: `true`
- `ExternalPorts` (IList<string>?): Nodes to expose as ports. Default: `null` (all non-ground nodes internal)

**Important:** For closed circuits (no external ports), set `ExternalPorts = new List<string>()` or `Array.Empty<string>()`.

### SkinType Enum

- `Digital`: Digital logic gates and components
- `Analog`: Analog components (resistors, capacitors, transistors, etc.)

## Usage Patterns

### Pattern 1: Simple SpiceSharp Circuit

```csharp
using NetlistSvg;
using SpiceSharp;
using SpiceSharp.Components;

var circuit = new Circuit(
    new VoltageSource("V1", "vcc", "0", 9.0),
    new Resistor("R1", "vcc", "out", 1e3),
    new Resistor("R2", "out", "0", 1e3)
);

using var renderer = new SchematicRenderer();
var svg = renderer.Render(circuit);

File.WriteAllText("output.svg", svg);
```

### Pattern 2: JSON Netlist

```csharp
using NetlistSvg;
using NetlistSvg.Skins;

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
var svg = renderer.Render(netlistJson, SkinType.Digital);
```

### Pattern 3: Async Rendering

```csharp
using var renderer = new SchematicRenderer();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var svg = await renderer.RenderAsync(circuit, cancellationToken: cts.Token);
```

### Pattern 4: Custom Options

```csharp
var options = new RenderOptions
{
    MaxMemory = 2_000_000_000,  // 2GB for large circuits
    Timeout = TimeSpan.FromMinutes(5),
    DefaultSkin = SkinType.Analog
};

using var renderer = new SchematicRenderer(options);
var svg = renderer.Render(circuit);
```

### Pattern 5: Closed Circuit (No External Ports)

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
    ExternalPorts = Array.Empty<string>()  // No external ports
});
```

## Supported Components

### SpiceSharp Component Mapping

| SpiceSharp Component | Symbol Type | Notes |
|---------------------|-------------|-------|
| `Resistor` | `r_h` | Horizontal resistor |
| `Capacitor` | `c_h` | Horizontal capacitor |
| `Inductor` | `l_h` | Horizontal inductor |
| `Diode` | `d_h` or `d_led_h` | Standard diode or LED (if model contains "LED") |
| `VoltageSource` | `vcc`/`gnd` | Converted to VCC and GND symbols |
| `BipolarJunctionTransistor` | `q_npn` or `q_pnp` | Based on model type |

### Component Value Display

When `SpiceRenderOptions.ShowValues = true` (default), component values are displayed using engineering notation:
- 1000 → "1.0k"
- 0.000001 → "1.0µ"
- 0.000000001 → "1.0n"

## Error Handling

### NetlistException

Thrown when rendering fails. Contains:
- `Message`: Human-readable error message
- `NetlistJson`: The netlist that failed (truncated if > 1000 chars)
- `JavaScriptError`: Original JavaScript error message

**Example:**
```csharp
try
{
    var svg = renderer.Render(circuit);
}
catch (NetlistException ex)
{
    Console.WriteLine($"Rendering failed: {ex.Message}");
    Console.WriteLine($"JavaScript error: {ex.JavaScriptError}");
}
```

### Common Errors

1. **MemoryLimitExceededException**: Increase `RenderOptions.MaxMemory`
2. **TimeoutException**: Increase `RenderOptions.Timeout` or optimize circuit
3. **InvalidOperationException**: Renderer not initialized (should not occur with public API)

## Performance Guidelines

1. **Memory**: Default 1GB handles most circuits. Only increase if needed.
2. **Concurrency**: Default 4 concurrent renders. Adjust based on workload.
3. **Disposal**: Always dispose `SchematicRenderer` when done to free resources.
4. **Async**: Use async methods for non-blocking operations.

## Dependencies

- **Jint** (3.1.2): JavaScript interpreter
- **Microsoft.Extensions.ObjectPool** (10.0.0): Object pooling
- **SpiceSharp** (3.2.3): Circuit modeling (optional, for SpiceSharp integration)

All JavaScript resources (ELK, netlistsvg) are embedded - no external dependencies.

## License Information

Access third-party license information:

```csharp
var licenses = LicenseInfo.GetThirdPartyLicenses();
Console.WriteLine(licenses);
```

Or via CLI:
```bash
netlistsvg --license
```

## Example: Complete Workflow

```csharp
using NetlistSvg;
using NetlistSvg.Skins;
using SpiceSharp;
using SpiceSharp.Components;

// 1. Create circuit
var circuit = new Circuit(
    new VoltageSource("V1", "vcc", "0", 9.0),
    new Resistor("R1", "vcc", "led_anode", 330),
    new Diode("D1", "led_anode", "0", "LED")
);

// 2. Configure options
var renderOptions = new SpiceRenderOptions
{
    Skin = SkinType.Analog,
    ShowValues = true,
    ExternalPorts = Array.Empty<string>()  // Closed circuit
};

// 3. Render to SVG
using var renderer = new SchematicRenderer();
var svg = renderer.Render(circuit, renderOptions);

// 4. Save or use SVG
File.WriteAllText("circuit.svg", svg);
```

## Notes for AI Agents

1. **Always dispose** `SchematicRenderer` instances (use `using` statement)
2. **Default memory (1GB)** is sufficient for most use cases
3. **Closed circuits** require `ExternalPorts = Array.Empty<string>()`
4. **LED detection** is automatic if diode model name contains "LED"
5. **VoltageSource** components are automatically converted to VCC/GND symbols
6. **Skin selection**: Use `SkinType.Digital` for logic circuits, `SkinType.Analog` for analog circuits
7. **Error messages** from `NetlistException` include JavaScript error details for debugging

## Quick Reference

**Minimal Example:**
```csharp
using var renderer = new SchematicRenderer();
var svg = renderer.Render(circuit);
```

**With Options:**
```csharp
using var renderer = new SchematicRenderer();
var svg = renderer.Render(circuit, new SpiceRenderOptions 
{ 
    Skin = SkinType.Analog,
    ExternalPorts = Array.Empty<string>()
});
```

**Async:**
```csharp
using var renderer = new SchematicRenderer();
var svg = await renderer.RenderAsync(circuit);
```

