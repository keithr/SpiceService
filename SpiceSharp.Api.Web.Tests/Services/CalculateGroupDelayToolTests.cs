using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for CalculateGroupDelay MCP tool
/// </summary>
public class CalculateGroupDelayToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public CalculateGroupDelayToolTests()
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
    public async Task ExecuteCalculateGroupDelay_WithValidInput_ReturnsPlot()
    {
        // Arrange: Create AC analysis result with phase data
        var circuitId = "test_gd";
        var frequencies = new List<double> { 100, 500, 1000, 2000, 5000, 10000 };
        
        // Create phase data for RC filter: phase = -arctan(ωRC)
        var realData = new List<double>();
        var imagData = new List<double>();
        
        foreach (var freq in frequencies)
        {
            var omegaRC = 2 * Math.PI * freq * 0.001; // RC = 0.001
            var phaseRad = -Math.Atan(omegaRC);
            var magnitude = 1.0 / Math.Sqrt(1.0 + omegaRC * omegaRC);
            realData.Add(magnitude * Math.Cos(phaseRad));
            imagData.Add(magnitude * Math.Sin(phaseRad));
        }
        
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "ac",
            XData = frequencies.ToArray(),
            XLabel = "Frequency (Hz)",
            Signals = new Dictionary<string, double[]>
            {
                { "v(out)", realData.ToArray() }
            },
            ImaginarySignals = new Dictionary<string, double[]>
            {
                { "v(out)", imagData.ToArray() }
            }
        };
        _resultsCache.Store(circuitId, cachedResult);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            signal = "v(out)",
            format = "png"
        });

        // Act: Calculate group delay via MCP tool
        var result = await _mcpService.ExecuteTool("calculate_group_delay", arguments);

        // Assert: Should return plot image
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        var imageContent = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.Equal("image/png", imageContent.MimeType);
        Assert.NotNull(imageContent.Data);
        
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var summary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(textContent.Text ?? "");
        Assert.NotNull(summary);
        Assert.True(summary.ContainsKey("signal"));
        Assert.Equal("v(out)", summary["signal"].GetString());
    }

    [Fact]
    public async Task ExecuteCalculateGroupDelay_WithInvalidSignal_ThrowsException()
    {
        // Arrange: Create cached result but with different signal
        var circuitId = "test_gd_invalid";
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "ac",
            XData = new[] { 100.0, 1000.0 },
            Signals = new Dictionary<string, double[]>
            {
                { "v(other)", new[] { 1.0, 0.5 } }
            },
            ImaginarySignals = new Dictionary<string, double[]>
            {
                { "v(other)", new[] { 0.0, -0.5 } }
            }
        };
        _resultsCache.Store(circuitId, cachedResult);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            signal = "v(nonexistent)"
        });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await _mcpService.ExecuteTool("calculate_group_delay", arguments));
    }

    [Fact]
    public async Task ExecuteCalculateGroupDelay_WithoutCircuitId_UsesActiveCircuit()
    {
        // Arrange: Create circuit and set as active
        var circuit = _circuitManager.CreateCircuit("test_gd_active", "Test");
        _circuitManager.SetActiveCircuit("test_gd_active");
        
        var frequencies = new[] { 100.0, 1000.0, 10000.0 };
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "ac",
            XData = frequencies,
            Signals = new Dictionary<string, double[]>
            {
                { "v(out)", new[] { 1.0, 0.5, 0.1 } }
            },
            ImaginarySignals = new Dictionary<string, double[]>
            {
                { "v(out)", new[] { 0.0, -0.5, -0.1 } }
            }
        };
        _resultsCache.Store("test_gd_active", cachedResult);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            signal = "v(out)"
        });

        // Act: Calculate group delay (no circuit_id)
        var result = await _mcpService.ExecuteTool("calculate_group_delay", arguments);

        // Assert: Should use active circuit and return plot
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
    }
}
