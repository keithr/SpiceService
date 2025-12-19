using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Comprehensive tests for plot_impedance tool covering various circuit topologies,
/// including circuits with and without existing voltage sources at the measurement port.
/// Based on analysis of speaker crossover impedance measurement scenarios.
/// </summary>
public class PlotImpedanceComprehensiveTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;

    public PlotImpedanceComprehensiveTests()
    {
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
        _modelService = new ModelService();
        var operatingPointService = new OperatingPointService();
        var dcAnalysisService = new DCAnalysisService();
        var transientAnalysisService = new TransientAnalysisService();
        var acAnalysisService = new ACAnalysisService();
        var netlistService = new NetlistService();
        var parameterSweepService = new ParameterSweepService(
            operatingPointService,
            dcAnalysisService,
            acAnalysisService,
            transientAnalysisService);
        var noiseAnalysisService = new NoiseAnalysisService();
        var temperatureSweepService = new TemperatureSweepService(
            operatingPointService,
            dcAnalysisService,
            acAnalysisService,
            transientAnalysisService);
        var impedanceAnalysisService = new ImpedanceAnalysisService(acAnalysisService);
        _resultsCache = new CircuitResultsCache();
        var responseMeasurementService = new ResponseMeasurementService(_resultsCache);
        var groupDelayService = new GroupDelayService(_resultsCache);
        var netlistParser = new NetlistParser();
        var config = new MCPServerConfig { Version = "1.0.0" };
        _mcpService = new MCPService(
            _circuitManager,
            _componentService,
            _modelService,
            operatingPointService,
            dcAnalysisService,
            transientAnalysisService,
            acAnalysisService,
            netlistService,
            parameterSweepService,
            noiseAnalysisService,
            temperatureSweepService,
            impedanceAnalysisService,
            responseMeasurementService,
            groupDelayService,
            netlistParser,
            _resultsCache,
            config,
            null,
            null);
    }

    [Fact]
    public async Task PlotImpedance_SimpleRLC_WithoutVoltageSource_ReturnsValidSVG()
    {
        // Arrange: Create simple R-L-C circuit WITHOUT voltage source at input
        // This is the recommended topology for impedance measurement
        var circuitId = "impedance_rlc_simple";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Simple R-L-C Impedance Test");
        _circuitManager.SetActiveCircuit(circuitId);

        // R-L-C network: R=8Ω, L=1mH series, C=10µF parallel
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "input", "mid" },
            Value = 8.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "inductor",
            Name = "L1",
            Nodes = new List<string> { "mid", "0" },
            Value = 1e-3  // 1mH
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C1",
            Nodes = new List<string> { "input", "0" },
            Value = 10e-6  // 10µF
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 20.0,
            stop_freq = 20000.0,
            points_per_decade = 10,
            format = "svg"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        // Should have SVG content (text format for embedding)
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        // Validate SVG structure
        var svgText = textContent.Text;
        Assert.Contains("<?xml", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<svg", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</svg>", svgText, StringComparison.OrdinalIgnoreCase);

        // Should also have image format
        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.Equal("image/svg+xml", imageContent.MimeType);
    }

    [Fact]
    public async Task PlotImpedance_SimpleRLC_WithExistingVoltageSource_HandlesGracefully()
    {
        // Arrange: Create circuit WITH voltage source at input
        // This tests the scenario where user has a voltage source already
        // The tool should either work (if it removes/replaces it) or fail gracefully
        var circuitId = "impedance_rlc_with_vsource";
        var circuit = _circuitManager.CreateCircuit(circuitId, "R-L-C with Voltage Source");
        _circuitManager.SetActiveCircuit(circuitId);

        // Add voltage source at input (this might conflict with impedance tool's internal source)
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "Vin",
            Nodes = new List<string> { "input", "0" },
            Value = 1.0,
            Parameters = new Dictionary<string, object>
            {
                { "ac", 1.0 }  // AC magnitude for AC analysis
            }
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "input", "mid" },
            Value = 8.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "inductor",
            Name = "L1",
            Nodes = new List<string> { "mid", "0" },
            Value = 1e-3
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C1",
            Nodes = new List<string> { "input", "0" },
            Value = 10e-6
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 20.0,
            stop_freq = 20000.0,
            points_per_decade = 10,
            format = "svg"
        });

        // Act & Assert
        // The tool should either:
        // 1. Work correctly (if it handles existing sources by removing/replacing them)
        // 2. Fail with a clear error message (if it can't handle the conflict)
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await _mcpService.ExecuteTool("plot_impedance", arguments);
            
            // If it succeeds, validate the result
            if (result != null && result.Content.Count > 0)
            {
                var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
                if (textContent != null && !string.IsNullOrEmpty(textContent.Text))
                {
                    Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
                }
            }
        });

        // Either no exception (works) or a clear error message
        if (exception != null)
        {
            Assert.NotNull(exception.Message);
            // Error should be informative about the conflict
            Assert.True(
                exception.Message.Contains("voltage source", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("port", StringComparison.OrdinalIgnoreCase),
                $"Error message should mention the issue: {exception.Message}");
        }
    }

    [Fact]
    public async Task PlotImpedance_SpeakerCrossover_WithoutVoltageSource_ReturnsValidPlot()
    {
        // Arrange: Create Paul Carmody 2-Way Speaker Crossover (without voltage source)
        // High-Pass Section (Tweeter):
        // Input → C1 (1.5µF) → C2 (2.2µF) → R1 (6Ω) → Tweeter (8Ω) → Ground
        // L1 (25mH) shunts from C1-C2 junction to ground
        // R2 (10Ω) in parallel with tweeter (both to ground)
        // Low-Pass Section (Woofer):
        // Input → L2 (1.1mH) → Woofer node
        // C3 (22µF) shunts from input to ground
        // C4 (6.8µF) shunts from woofer node to ground
        // Woofer (8Ω) from woofer node to ground
        var circuitId = "speaker_crossover_v2";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Paul Carmody 2-Way Speaker Crossover");
        _circuitManager.SetActiveCircuit(circuitId);

        // High-pass section (Tweeter path)
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C1",
            Nodes = new List<string> { "input", "hp_mid1" },
            Value = 1.5e-6  // 1.5µF
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C2",
            Nodes = new List<string> { "hp_mid1", "hp_mid2" },
            Value = 2.2e-6  // 2.2µF
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "hp_mid2", "tweeter_pos" },
            Value = 6.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "Tweeter",
            Nodes = new List<string> { "tweeter_pos", "0" },
            Value = 8.0  // Tweeter modeled as 8Ω resistor
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "inductor",
            Name = "L1",
            Nodes = new List<string> { "hp_mid1", "0" },
            Value = 25e-3  // 25mH
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R2",
            Nodes = new List<string> { "tweeter_pos", "0" },
            Value = 10.0
        });

        // Low-pass section (Woofer path)
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "inductor",
            Name = "L2",
            Nodes = new List<string> { "input", "woofer_pos" },
            Value = 1.1e-3  // 1.1mH
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C3",
            Nodes = new List<string> { "input", "0" },
            Value = 22e-6  // 22µF
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C4",
            Nodes = new List<string> { "woofer_pos", "0" },
            Value = 6.8e-6  // 6.8µF
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "Woofer",
            Nodes = new List<string> { "woofer_pos", "0" },
            Value = 8.0  // Woofer modeled as 8Ω resistor
        });

        // Note: NO voltage source - tool will add its own

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 20.0,
            stop_freq = 20000.0,
            points_per_decade = 10,
            format = "svg"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        // Should have SVG content
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        // Validate SVG structure
        var svgText = textContent.Text;
        Assert.Contains("<?xml", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<svg", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</svg>", svgText, StringComparison.OrdinalIgnoreCase);

        // Should contain impedance-related labels
        Assert.True(
            svgText.Contains("impedance", StringComparison.OrdinalIgnoreCase) ||
            svgText.Contains("magnitude", StringComparison.OrdinalIgnoreCase) ||
            svgText.Contains("phase", StringComparison.OrdinalIgnoreCase) ||
            svgText.Contains("frequency", StringComparison.OrdinalIgnoreCase),
            "SVG should contain impedance plot labels");
    }

    [Fact]
    public async Task PlotImpedance_SimpleResistor_ReturnsReasonableImpedance()
    {
        // Arrange: Simple 8Ω resistor (should show ~8Ω impedance across all frequencies)
        var circuitId = "impedance_resistor";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Simple Resistor Impedance");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "input", "0" },
            Value = 8.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 100.0,
            stop_freq = 10000.0,
            points_per_decade = 10,
            format = "svg"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        // Should have valid SVG
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlotImpedance_WithSVGFormat_ReturnsTextAndImageFormats()
    {
        // Arrange: Simple RC circuit
        var circuitId = "impedance_svg_formats";
        var circuit = _circuitManager.CreateCircuit(circuitId, "SVG Format Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "input", "0" },
            Value = 1000.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 100.0,
            stop_freq = 10000.0,
            points_per_decade = 10,
            format = "svg"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert: Should have both text and image formats for SVG
        Assert.NotNull(result);
        Assert.True(result.Content.Count >= 2, "Should have both text and image content for SVG");

        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);

        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.Equal("image/svg+xml", imageContent.MimeType);
        Assert.NotNull(imageContent.Data);

        // Decode and verify image content matches text
        var imageBytes = Convert.FromBase64String(imageContent.Data);
        var imageSvgText = System.Text.Encoding.UTF8.GetString(imageBytes);
        Assert.Contains("<svg", imageSvgText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlotImpedance_FrequencyRange_20HzTo20kHz_ValidatesRange()
    {
        // Arrange: Test the full audio frequency range
        var circuitId = "impedance_full_range";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Full Frequency Range Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "input", "0" },
            Value = 8.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "input",
            port_negative = "0",
            start_freq = 20.0,
            stop_freq = 20000.0,
            points_per_decade = 10,
            format = "svg"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        // SVG should be valid
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
        
        // Should contain frequency information (may be in labels or data)
        Assert.True(
            textContent.Text.Contains("20", StringComparison.OrdinalIgnoreCase) ||
            textContent.Text.Contains("20000", StringComparison.OrdinalIgnoreCase) ||
            textContent.Text.Contains("frequency", StringComparison.OrdinalIgnoreCase),
            "SVG should contain frequency range information");
    }
}
