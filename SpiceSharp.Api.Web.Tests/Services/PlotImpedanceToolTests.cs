using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for PlotImpedance MCP tool
/// </summary>
public class PlotImpedanceToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public PlotImpedanceToolTests()
    {
        _circuitManager = new CircuitManager();
        var componentService = new ComponentService();
        var modelService = new ModelService();
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
            componentService,
            modelService,
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
    public async Task ExecutePlotImpedance_WithValidInput_ReturnsPlot()
    {
        // Arrange: Create a simple RC circuit
        var circuitId = "test_impedance1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "RC circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        
        // Add resistor R1 (1kΩ)
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        componentService.AddComponent(circuit, r1Def);

        // Add capacitor C1 (1µF)
        var c1Def = new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6
        };
        componentService.AddComponent(circuit, c1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "in",
            port_negative = "0",
            start_freq = 100.0,
            stop_freq = 10000.0,
            points_per_decade = 10,
            format = "png"  // Use PNG as default since it works better in MCP clients
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        // Should have image content (PNG)
        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.NotNull(imageContent.Data);
        Assert.Equal("image/png", imageContent.MimeType);
        
        // Verify base64 data can be decoded
        var imageBytes = Convert.FromBase64String(imageContent.Data);
        Assert.True(imageBytes.Length > 0, "Image data should not be empty");
    }

    [Fact]
    public async Task ExecutePlotImpedance_WithSVGFormat_ReturnsValidSVG()
    {
        // Arrange: Create a simple RC circuit
        var circuitId = "test_impedance_svg";
        var circuit = _circuitManager.CreateCircuit(circuitId, "RC circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        
        // Add resistor R1 (1kΩ)
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        componentService.AddComponent(circuit, r1Def);

        // Add capacitor C1 (1µF)
        var c1Def = new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6
        };
        componentService.AddComponent(circuit, c1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "in",
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
        
        // Should have image content (SVG as base64)
        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.NotNull(imageContent.Data);
        Assert.Equal("image/svg+xml", imageContent.MimeType);
        
        // Verify base64 data can be decoded
        byte[] svgBytes;
        try
        {
            svgBytes = Convert.FromBase64String(imageContent.Data);
            Assert.True(svgBytes.Length > 0, "SVG data should not be empty");
        }
        catch (FormatException ex)
        {
            Assert.Fail($"SVG base64 data is invalid: {ex.Message}");
            return;
        }
        
        // Verify decoded data is valid UTF-8 and contains SVG markers
        var svgText = System.Text.Encoding.UTF8.GetString(svgBytes);
        Assert.NotNull(svgText);
        Assert.True(svgText.Length > 0, "SVG text should not be empty");
        
        // Verify it's actually SVG (contains SVG XML markers)
        Assert.Contains("<svg", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("xmlns", svgText, StringComparison.OrdinalIgnoreCase);
        
        // Should also have text content for SVG (alternative format)
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text" && c.MimeType == "image/svg+xml");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);
        Assert.Contains("<svg", textContent.Text, StringComparison.OrdinalIgnoreCase);
        
        // Verify both formats contain the same SVG content
        Assert.Equal(svgText, textContent.Text);
    }

    [Fact]
    public async Task ExecutePlotImpedance_WithIsolatedPort_ReturnsPlot()
    {
        // Arrange: Create an empty circuit (no components)
        // Adding a voltage source will create the nodes, so this should work
        // The impedance will be infinite or very high (isolated port)
        var circuitId = "test_impedance2";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Empty circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            port_positive = "isolated_node",
            port_negative = "0"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert: Should return a plot (impedance will be infinite/high for isolated port)
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        // Should have image content
        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
    }

    [Fact]
    public async Task ExecutePlotImpedance_WithoutCircuitId_UsesActiveCircuit()
    {
        // Arrange
        var circuitId = "test_impedance3";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "0" },
            Value = 1000.0
        };
        componentService.AddComponent(circuit, r1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            port_positive = "in",
            port_negative = "0"
        });

        // Act
        var result = await _mcpService.ExecuteTool("plot_impedance", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
    }
}
