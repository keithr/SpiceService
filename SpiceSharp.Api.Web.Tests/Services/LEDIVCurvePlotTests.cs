using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for plotting LED I-V curves in both SVG and PNG formats
/// </summary>
public class LEDIVCurvePlotTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;

    public LEDIVCurvePlotTests()
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
    public async Task PlotLEDIVCurve_WithPNGFormat_ReturnsValidPNG()
    {
        // Arrange: Create a red LED circuit
        var circuitId = "led_iv_png";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Red LED I-V Curve Test (PNG)");
        _circuitManager.SetActiveCircuit(circuitId);

        // Define red LED model (typical parameters for a red LED)
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "RED_LED",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-14 },      // Saturation current (A)
                { "N", 1.8 },         // Emission coefficient
                { "RS", 0.5 },        // Series resistance (Ω)
                { "BV", 5.0 },        // Reverse breakdown voltage (V)
                { "IBV", 1e-6 }       // Reverse breakdown current (A)
            }
        });

        // Add LED diode
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "0" },
            Model = "RED_LED"
        });

        // Add voltage source for DC sweep
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "anode", "0" },
            Value = 0.0  // Will be swept
        });

        // Run DC sweep from 0V to 3V (typical LED forward voltage range)
        // For 50 points from start to stop, we need 49 steps: step = (stop - start) / (points - 1)
        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 3.0,
            step = (3.0 - 0.0) / 49.0,  // 49 steps gives 50 points (includes start and stop)
            exports = new[] { "i(V1)" }  // Current through voltage source
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot I-V curve with PNG format, using invert_signals to show conventional direction
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "i(V1)" },
            invert_signals = new[] { "i(V1)" },  // Invert to show positive current flow (SPICE convention makes it negative)
            image_format = "png",
            output_format = new[] { "image" },
            options = new
            {
                title = "Red LED I-V Characteristic",
                x_label = "Voltage (V)",
                y_label = "Current (A)",
                width = 800,
                height = 600
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        // Should have image content (PNG)
        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.NotNull(imageContent.Data);
        Assert.Equal("image/png", imageContent.MimeType);

        // Verify base64 data can be decoded
        Assert.NotNull(imageContent.Data);
        var imageBytes = Convert.FromBase64String(imageContent.Data);
        Assert.True(imageBytes.Length > 0, "PNG image data should not be empty");
        Assert.True(imageBytes.Length > 1000, "PNG image should have reasonable size (at least 1KB)");

        // Verify PNG header (starts with PNG signature: 89 50 4E 47 0D 0A 1A 0A)
        var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var actualHeader = imageBytes.Take(8).ToArray();
        Assert.Equal(pngSignature, actualHeader);
    }

    [Fact]
    public async Task PlotLEDIVCurve_WithSVGFormat_ReturnsValidSVG()
    {
        // Arrange: Create a red LED circuit
        var circuitId = "led_iv_svg";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Red LED I-V Curve Test (SVG)");
        _circuitManager.SetActiveCircuit(circuitId);

        // Define red LED model (typical parameters for a red LED)
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "RED_LED",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-14 },      // Saturation current (A)
                { "N", 1.8 },         // Emission coefficient
                { "RS", 0.5 },        // Series resistance (Ω)
                { "BV", 5.0 },        // Reverse breakdown voltage (V)
                { "IBV", 1e-6 }       // Reverse breakdown current (A)
            }
        });

        // Add LED diode
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "0" },
            Model = "RED_LED"
        });

        // Add voltage source for DC sweep
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "anode", "0" },
            Value = 0.0  // Will be swept
        });

        // Run DC sweep from 0V to 3V (typical LED forward voltage range)
        // For 50 points from start to stop, we need 49 steps: step = (stop - start) / (points - 1)
        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 3.0,
            step = (3.0 - 0.0) / 49.0,  // 49 steps gives 50 points (includes start and stop)
            exports = new[] { "i(V1)" }  // Current through voltage source
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Plot I-V curve with SVG format, using invert_signals to show conventional direction
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "i(V1)" },
            invert_signals = new[] { "i(V1)" },  // Invert to show positive current flow (SPICE convention makes it negative)
            image_format = "svg",
            output_format = new[] { "image", "text" },  // Get both base64 and raw SVG text
            options = new
            {
                title = "Red LED I-V Characteristic",
                x_label = "Voltage (V)",
                y_label = "Current (A)",
                width = 800,
                height = 600
            }
        });

        var result = await _mcpService.ExecuteTool("plot_results", plotArgs);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);

        // Should have image content (SVG as base64)
        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.NotNull(imageContent.Data);
        Assert.Equal("image/svg+xml", imageContent.MimeType);

        // Verify base64 data can be decoded
        Assert.NotNull(imageContent.Data);
        var svgBytes = Convert.FromBase64String(imageContent.Data);
        Assert.True(svgBytes.Length > 0, "SVG image data should not be empty");

        // Decode and verify SVG content
        var svgText = System.Text.Encoding.UTF8.GetString(svgBytes);
        Assert.NotNull(svgText);
        Assert.True(svgText.Length > 100, "SVG should have reasonable content length");

        // Verify SVG structure
        Assert.StartsWith("<?xml", svgText.TrimStart());
        Assert.Contains("<svg", svgText);
        Assert.Contains("</svg>", svgText);
        Assert.Contains("Red LED I-V Characteristic", svgText);  // Title should be in SVG
        Assert.Contains("Voltage (V)", svgText);  // X-axis label
        Assert.Contains("Current (A)", svgText);  // Y-axis label

        // Optionally check for text format output if available
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        if (textContent != null)
        {
            Assert.NotNull(textContent.Text);
            Assert.Contains("<svg", textContent.Text);
        }
    }

    [Fact]
    public async Task PlotLEDIVCurve_BothFormats_ProduceValidOutputs()
    {
        // Arrange: Create a red LED circuit
        var circuitId = "led_iv_both";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Red LED I-V Curve Test (Both Formats)");
        _circuitManager.SetActiveCircuit(circuitId);

        // Define red LED model
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "RED_LED",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-14 },
                { "N", 1.8 },
                { "RS", 0.5 }
            }
        });

        // Add LED and voltage source
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "0" },
            Model = "RED_LED"
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "anode", "0" },
            Value = 0.0
        });

        // Run DC sweep
        var dcArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            source = "V1",
            start = 0.0,
            stop = 3.0,
            step = (3.0 - 0.0) / 49.0,  // 49 steps gives 50 points (includes start and stop)
            exports = new[] { "i(V1)" }
        });

        await _mcpService.ExecuteTool("run_dc_analysis", dcArgs);

        // Act: Test PNG format
        var pngPlotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "i(V1)" },
            invert_signals = new[] { "i(V1)" },  // Invert to show positive current flow
            image_format = "png",
            output_format = new[] { "image" },
            options = new
            {
                title = "Red LED I-V Characteristic (PNG)",
                x_label = "Voltage (V)",
                y_label = "Current (A)"
            }
        });

        var pngResult = await _mcpService.ExecuteTool("plot_results", pngPlotArgs);

        // Act: Test SVG format
        var svgPlotArgs = JsonSerializer.SerializeToElement(new
        {
            signals = new[] { "i(V1)" },
            invert_signals = new[] { "i(V1)" },  // Invert to show positive current flow
            image_format = "svg",
            output_format = new[] { "image" },
            options = new
            {
                title = "Red LED I-V Characteristic (SVG)",
                x_label = "Voltage (V)",
                y_label = "Current (A)"
            }
        });

        var svgResult = await _mcpService.ExecuteTool("plot_results", svgPlotArgs);

        // Assert: PNG format
        Assert.NotNull(pngResult);
        var pngImage = pngResult.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(pngImage);
        Assert.NotNull(pngImage.Data);
        Assert.Equal("image/png", pngImage.MimeType);
        var pngBytes = Convert.FromBase64String(pngImage.Data);
        Assert.True(pngBytes.Length > 1000, "PNG should have reasonable size");

        // Verify PNG signature
        var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.Equal(pngSignature, pngBytes.Take(8).ToArray());

        // Assert: SVG format
        Assert.NotNull(svgResult);
        var svgImage = svgResult.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(svgImage);
        Assert.NotNull(svgImage.Data);
        Assert.Equal("image/svg+xml", svgImage.MimeType);
        var svgBytes = Convert.FromBase64String(svgImage.Data);
        var svgText = System.Text.Encoding.UTF8.GetString(svgBytes);
        Assert.Contains("<svg", svgText);
        Assert.Contains("</svg>", svgText);
        Assert.Contains("Red LED I-V Characteristic (SVG)", svgText);
    }
}
