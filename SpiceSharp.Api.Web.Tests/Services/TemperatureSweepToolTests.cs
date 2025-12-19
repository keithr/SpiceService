using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for TemperatureSweep MCP tool
/// </summary>
public class TemperatureSweepToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly ITemperatureSweepService _temperatureSweepService;
    private readonly CircuitResultsCache _resultsCache;

    public TemperatureSweepToolTests()
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
        _temperatureSweepService = new TemperatureSweepService(
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
            _temperatureSweepService,
            impedanceAnalysisService,
            responseMeasurementService,
            groupDelayService,
            netlistParser,
            _resultsCache,
            config);
    }

    [Fact]
    public async Task ExecuteTemperatureSweep_WithValidInput_ReturnsResults()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        // Add a simple resistor circuit
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        var componentService = new ComponentService();
        componentService.AddComponent(circuit, r1Def);

        var v1Def = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        };
        componentService.AddComponent(circuit, v1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            start_temp = 0.0,
            stop_temp = 50.0,
            points = 5,
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_temperature_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var response = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
        Assert.True(response.TryGetProperty("temperature_values", out var tempValues));
        Assert.True(response.TryGetProperty("results", out var results));
        Assert.True(response.TryGetProperty("status", out var status));
        Assert.Equal("Success", status.GetString());
        
        // Verify temperature values
        var tempArray = tempValues.EnumerateArray().ToList();
        Assert.Equal(5, tempArray.Count);
        Assert.Equal(0.0, tempArray[0].GetDouble(), 1);
        Assert.Equal(50.0, tempArray[4].GetDouble(), 1);
    }

    [Fact]
    public async Task ExecuteTemperatureSweep_WithInvalidAnalysis_ThrowsException()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            start_temp = 0.0,
            stop_temp = 50.0,
            points = 5,
            analysis_type = "invalid_analysis",
            outputs = new[] { "v(out)" }
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _mcpService.ExecuteTool("run_temperature_sweep", arguments));
        
        Assert.Contains("Unsupported analysis type", exception.Message);
    }

    [Fact]
    public async Task ExecuteTemperatureSweep_StoresResultsInCache()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        var componentService = new ComponentService();
        componentService.AddComponent(circuit, r1Def);

        var v1Def = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        };
        componentService.AddComponent(circuit, v1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            start_temp = 0.0,
            stop_temp = 50.0,
            points = 5,
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act
        await _mcpService.ExecuteTool("run_temperature_sweep", arguments);

        // Assert
        var cachedResult = _resultsCache.Get(circuitId);
        Assert.NotNull(cachedResult);
        Assert.Equal("temperature_sweep", cachedResult.AnalysisType);
        Assert.Equal("Temperature (°C)", cachedResult.XLabel);
        Assert.True(cachedResult.Signals.ContainsKey("v(out)"));
    }

    [Fact]
    public async Task ExecuteTemperatureSweep_WithoutCircuitId_UsesActiveCircuit()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        var componentService = new ComponentService();
        componentService.AddComponent(circuit, r1Def);

        var v1Def = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        };
        componentService.AddComponent(circuit, v1Def);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            start_temp = 0.0,
            stop_temp = 50.0,
            points = 5,
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act
        var result = await _mcpService.ExecuteTool("run_temperature_sweep", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
    }
}
