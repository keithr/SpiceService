using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for MeasureResponse MCP tool
/// </summary>
public class MeasureResponseToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public MeasureResponseToolTests()
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
    public async Task ExecuteMeasureResponse_WithBandwidth3dB_ReturnsFrequency()
    {
        // Arrange: Create AC analysis result with -3dB point at 1000 Hz
        var circuitId = "test_bw";
        var frequencies = new List<double> { 100, 500, 1000, 2000, 5000, 10000 };
        var magnitudeDb = new List<double> { 0, -1, -3, -6, -12, -18 }; // -3dB at 1000 Hz
        
        // Convert magnitude dB to complex for AC analysis format
        var realData = new List<double>();
        var imagData = new List<double>();
        for (int i = 0; i < magnitudeDb.Count; i++)
        {
            var magnitude = Math.Pow(10, magnitudeDb[i] / 20.0);
            realData.Add(magnitude); // Real part = magnitude (phase = 0 for simplicity)
            imagData.Add(0.0);
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

        var arguments = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            measurement = "bandwidth_3db",
            signal = "v(out)"
        });

        // Act: Measure -3dB bandwidth via MCP tool
        var result = await _mcpService.ExecuteTool("measure_response", arguments);

        // Assert: Should return measurement result
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        var content = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(content);
        var response = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content.Text ?? "");
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("value"));
        Assert.True(response.ContainsKey("unit"));
        Assert.Equal("Hz", response["unit"]?.ToString());
    }

    [Fact]
    public async Task ExecuteMeasureResponse_WithInvalidMeasurement_ThrowsException()
    {
        // Arrange
        var circuitId = "test_invalid";
        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            measurement = "invalid_measurement",
            signal = "v(out)"
        });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await _mcpService.ExecuteTool("measure_response", arguments));
    }

    [Fact]
    public async Task ExecuteMeasureResponse_WithGainAtFreq_ReturnsCorrectGain()
    {
        // Arrange: Create AC analysis result with complex values (as stored by run_ac_analysis)
        var circuitId = "test_gain_freq";
        var frequencies = new[] { 100.0, 1000.0, 5000.0 };
        
        // Convert magnitude dB to complex: magnitude = 10^(dB/20), phase = 0 for simplicity
        var magnitudeDb = new[] { 0.0, -6.0, -12.0 };
        var realData = new List<double>();
        var imagData = new List<double>();
        for (int i = 0; i < magnitudeDb.Length; i++)
        {
            var magnitude = Math.Pow(10, magnitudeDb[i] / 20.0);
            realData.Add(magnitude);
            imagData.Add(0.0);
        }
        
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "ac",
            XData = frequencies,
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
            measurement = "gain_at_freq",
            signal = "v(out)",
            frequency = 1000.0
        });

        // Act
        var result = await _mcpService.ExecuteTool("measure_response", arguments);

        // Assert: Should return approximately -6 dB
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        var content = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(content);
        var response = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content.Text ?? "");
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("value"));
        Assert.True(response.ContainsKey("unit"));
        Assert.Equal("dB", response["unit"].GetString());
        
        var value = response["value"].GetDouble();
        Assert.True(Math.Abs(value - (-6.0)) < 0.5, 
            $"Expected gain around -6 dB at 1000 Hz, got {value} dB");
    }
}
