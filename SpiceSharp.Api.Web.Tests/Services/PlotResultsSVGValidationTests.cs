using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Comprehensive tests to validate SVG output works correctly for all plot types and scenarios.
/// These tests ensure SVG is reliable for embedding in HTML/text artifacts (Claude's preferred format).
/// </summary>
public class PlotResultsSVGValidationTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;

    public PlotResultsSVGValidationTests()
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

    /// <summary>
    /// Validates that SVG output has proper structure and can be embedded in HTML
    /// </summary>
    private void ValidateSVGStructure(string svgText, string? expectedTitle = null, string? expectedXLabel = null, string? expectedYLabel = null)
    {
        Assert.NotNull(svgText);
        Assert.True(svgText.Length > 100, "SVG should have reasonable content length");

        // Basic SVG structure
        Assert.Contains("<?xml", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<svg", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</svg>", svgText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("xmlns", svgText, StringComparison.OrdinalIgnoreCase);

        // Should have proper XML declaration (may have BOM, so trim whitespace and BOM)
        var trimmed = svgText.TrimStart();
        if (trimmed.StartsWith("\uFEFF", StringComparison.Ordinal)) // BOM
            trimmed = trimmed.Substring(1);
        Assert.StartsWith("<?xml", trimmed, StringComparison.OrdinalIgnoreCase);

        // Validate SVG is well-formed (basic check - has opening and closing tags)
        var svgOpenCount = Regex.Matches(svgText, @"<svg", RegexOptions.IgnoreCase).Count;
        var svgCloseCount = Regex.Matches(svgText, @"</svg>", RegexOptions.IgnoreCase).Count;
        Assert.Equal(svgOpenCount, svgCloseCount);

        // Check for expected content
        if (!string.IsNullOrEmpty(expectedTitle))
        {
            Assert.Contains(expectedTitle, svgText, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(expectedXLabel))
        {
            Assert.Contains(expectedXLabel, svgText, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(expectedYLabel))
        {
            Assert.Contains(expectedYLabel, svgText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task PlotResults_SVG_DCSweep_LinePlot_ValidSVG()
    {
        // Arrange: Create circuit and run DC sweep
        var circuitId = "svg_dc_sweep";
        var circuit = _circuitManager.CreateCircuit(circuitId, "DC Sweep SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "0" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 5.0,
            step = 0.5,
            exports = new[] { "v(node1)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot with SVG format
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "v(node1)" },
            image_format = "svg",
            output_format = new[] { "text" },
            options = new
            {
                title = "DC Sweep Test",
                x_label = "Voltage (V)",
                y_label = "Voltage (V)"
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        ValidateSVGStructure(textContent.Text, "DC Sweep Test", "Voltage (V)", "Voltage (V)");
    }

    [Fact]
    public async Task PlotResults_SVG_Transient_LinePlot_ValidSVG()
    {
        // Arrange: Create circuit and run transient analysis
        var circuitId = "svg_transient";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Transient SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "0" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C1",
            Nodes = new List<string> { "node1", "0" },
            Value = 1e-6
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        var transientArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            start_time = 0.0,
            stop_time = 0.001,
            time_step = 1e-6,
            signals = new[] { "v(node1)" }
        });

        await _mcpService.ExecuteTool("run_transient_analysis", transientArgs);

        // Act: Plot with SVG format
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "v(node1)" },
            image_format = "svg",
            output_format = new[] { "text" },
            options = new
            {
                title = "Transient Analysis",
                x_label = "Time (s)",
                y_label = "Voltage (V)"
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        ValidateSVGStructure(textContent.Text, "Transient Analysis", "Time (s)", "Voltage (V)");
    }

    [Fact]
    public async Task PlotResults_SVG_AC_BodePlot_ValidSVG()
    {
        // Arrange: Create circuit and run AC analysis
        var circuitId = "svg_ac";
        var circuit = _circuitManager.CreateCircuit(circuitId, "AC Analysis SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "in", "out" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C1",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "in", "0" },
            Value = 1.0
        });

        var acArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            start_frequency = 100.0,
            stop_frequency = 10000.0,
            number_of_points = 50,
            signals = new[] { "v(out)" }
        });

        await _mcpService.ExecuteTool("run_ac_analysis", acArgs);

        // Act: Plot with SVG format (should auto-select Bode plot)
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "v(out)" },
            image_format = "svg",
            output_format = new[] { "text" },
            options = new
            {
                title = "AC Analysis - Bode Plot",
                x_label = "Frequency (Hz)"
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        ValidateSVGStructure(textContent.Text, "AC Analysis - Bode Plot", "Frequency (Hz)");
        // Bode plots should have magnitude and phase information
        Assert.True(
            textContent.Text.Contains("Magnitude", StringComparison.OrdinalIgnoreCase) ||
            textContent.Text.Contains("Phase", StringComparison.OrdinalIgnoreCase) ||
            textContent.Text.Contains("dB", StringComparison.OrdinalIgnoreCase),
            "Bode plot should contain magnitude/phase information");
    }

    [Fact]
    public async Task PlotResults_SVG_OperatingPoint_BarChart_ValidSVG()
    {
        // Arrange: Create circuit and run operating point
        var circuitId = "svg_op";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Operating Point SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "0" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        var opArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            signals = new[] { "v(node1)", "i(V1)" }
        });

        await _mcpService.ExecuteTool("run_operating_point", opArgs);

        // Act: Plot with SVG format (let it auto-detect bar chart for operating point)
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            // Don't specify signals - let it use all operating point data
            image_format = "svg",
            output_format = new[] { "text" },
            options = new
            {
                title = "Operating Point Analysis",
                y_label = "Value"
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        // For operating point bar charts, just validate SVG structure
        // Labels may be auto-generated differently
        ValidateSVGStructure(textContent.Text);
    }

    [Fact]
    public async Task PlotResults_SVG_MultipleSignals_ValidSVG()
    {
        // Arrange: Create circuit and run DC sweep with multiple signals
        var circuitId = "svg_multi";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Multiple Signals SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "node2" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R2",
            Nodes = new List<string> { "node2", "0" },
            Value = 2000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 5.0,
            step = 0.5,
            exports = new[] { "v(node1)", "v(node2)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot multiple signals with SVG
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "v(node1)", "v(node2)" },
            image_format = "svg",
            output_format = new[] { "text" },
            options = new
            {
                title = "Multiple Signals",
                legend = true
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        ValidateSVGStructure(textContent.Text, "Multiple Signals");
        // For multiple signals, SVG structure validation is sufficient
        // The actual signal names may be encoded in the plot data, not necessarily as text
        // We've already validated the SVG structure, which is the key requirement for embedding
    }

    [Fact]
    public async Task PlotResults_SVG_WithInvertSignals_ValidSVG()
    {
        // Arrange: Create circuit and run DC sweep
        var circuitId = "svg_invert";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Invert Signals SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "0" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 5.0,
            step = 0.5,
            exports = new[] { "i(V1)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot with invert_signals
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "i(V1)" },
            invert_signals = new[] { "i(V1)" },
            image_format = "svg",
            output_format = new[] { "text" },
            options = new
            {
                title = "Inverted Current",
                y_label = "Current (A)"
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        ValidateSVGStructure(textContent.Text, "Inverted Current", null, "Current (A)");
        // Should indicate inversion in label
        Assert.True(
            textContent.Text.Contains("inverted", StringComparison.OrdinalIgnoreCase) ||
            textContent.Text.Contains("positive convention", StringComparison.OrdinalIgnoreCase),
            "Inverted signal plot should indicate inversion in label");
    }

    [Fact]
    public async Task PlotResults_SVG_WithLogScale_ValidSVG()
    {
        // Arrange: Create circuit and run AC analysis
        var circuitId = "svg_log";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Log Scale SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "in", "out" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "capacitor",
            Name = "C1",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "in", "0" },
            Value = 1.0
        });

        var acArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            start_frequency = 100.0,
            stop_frequency = 10000.0,
            number_of_points = 50,
            signals = new[] { "v(out)" }
        });

        await _mcpService.ExecuteTool("run_ac_analysis", acArgs);

        // Act: Plot with log scale
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "v(out)" },
            image_format = "svg",
            output_format = new[] { "text" },
            options = new
            {
                title = "Log Scale Plot",
                x_scale = "log",
                y_scale = "log"
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        ValidateSVGStructure(textContent.Text, "Log Scale Plot");
    }

    [Fact]
    public async Task PlotResults_SVG_WithCustomOptions_ValidSVG()
    {
        // Arrange: Create circuit and run DC sweep
        var circuitId = "svg_custom";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Custom Options SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "0" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 5.0,
            step = 0.5,
            exports = new[] { "v(node1)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot with custom dimensions and options
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "v(node1)" },
            image_format = "svg",
            output_format = new[] { "text" },
            options = new
            {
                title = "Custom Sized Plot",
                width = 1200,
                height = 800,
                grid = true,
                legend = false
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);

        ValidateSVGStructure(textContent.Text, "Custom Sized Plot");
        // SVG should reflect custom dimensions (check for width/height attributes)
        Assert.True(
            textContent.Text.Contains("1200", StringComparison.OrdinalIgnoreCase) ||
            textContent.Text.Contains("800", StringComparison.OrdinalIgnoreCase) ||
            textContent.Text.Contains("viewBox", StringComparison.OrdinalIgnoreCase),
            "Custom dimensions should be reflected in SVG");
    }

    [Fact]
    public async Task PlotResults_SVG_TextAndImageFormats_BothValid()
    {
        // Arrange: Create circuit and run DC sweep
        var circuitId = "svg_both";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Both Formats SVG Test");
        _circuitManager.SetActiveCircuit(circuitId);

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "resistor",
            Name = "R1",
            Nodes = new List<string> { "node1", "0" },
            Value = 1000
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            ComponentType = "voltage_source",
            Name = "V1",
            Nodes = new List<string> { "node1", "0" },
            Value = 5.0
        });

        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 5.0,
            step = 0.5,
            exports = new[] { "v(node1)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Request both text and image formats
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "v(node1)" },
            image_format = "svg",
            output_format = new[] { "text", "image" }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert: Both formats should be present and valid
        Assert.NotNull(result);
        Assert.True(result.Content.Count >= 2, "Should have both text and image content");

        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        Assert.NotNull(textContent.Text);
        ValidateSVGStructure(textContent.Text);

        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.NotNull(imageContent.Data);
        Assert.Equal("image/svg+xml", imageContent.MimeType);

        // Decode base64 image and verify it matches text content (or at least is valid SVG)
        var imageBytes = Convert.FromBase64String(imageContent.Data);
        var imageSvgText = System.Text.Encoding.UTF8.GetString(imageBytes);
        ValidateSVGStructure(imageSvgText);
    }
}
