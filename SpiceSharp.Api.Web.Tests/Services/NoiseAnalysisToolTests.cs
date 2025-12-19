using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for NoiseAnalysis MCP tool - verifies that the tool is not exposed since SpiceSharp doesn't support noise analysis
/// </summary>
public class NoiseAnalysisToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly INoiseAnalysisService _noiseAnalysisService;
    private readonly CircuitResultsCache _resultsCache;

    public NoiseAnalysisToolTests()
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
        _noiseAnalysisService = new NoiseAnalysisService();
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
            _noiseAnalysisService,
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
    public async Task ExecuteNoiseAnalysis_WithValidInput_ThrowsArgumentException()
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
            output_node = "out",
            input_source = "V1",
            start_freq = 20.0,
            stop_freq = 20000.0,
            points_per_decade = 10
        });

        // Act & Assert
        // Noise analysis tool is not exposed because SpiceSharp doesn't support it
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _mcpService.ExecuteTool("run_noise_analysis", arguments));
        
        Assert.Contains("Unknown tool: run_noise_analysis", exception.Message);
    }

    [Fact]
    public async Task ExecuteNoiseAnalysis_WithInvalidNode_ThrowsArgumentException()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            output_node = "nonexistent",
            input_source = "V1"
        });

        // Act & Assert
        // Noise analysis tool is not exposed because SpiceSharp doesn't support it
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _mcpService.ExecuteTool("run_noise_analysis", arguments));
        
        Assert.Contains("Unknown tool: run_noise_analysis", exception.Message);
    }

    [Fact]
    public async Task ExecuteNoiseAnalysis_WithMissingOutputNode_ThrowsArgumentException()
    {
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            input_source = "V1"
        });

        // Act & Assert
        // Noise analysis tool is not exposed because SpiceSharp doesn't support it
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _mcpService.ExecuteTool("run_noise_analysis", arguments));
        
        Assert.Contains("Unknown tool: run_noise_analysis", exception.Message);
    }

    [Fact]
    public async Task ExecuteNoiseAnalysis_WithoutCircuitId_ThrowsArgumentException()
    {
        // Arrange
        var circuitId = "test_circuit";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            output_node = "out",
            input_source = "V1"
        });

        // Act & Assert
        // Noise analysis tool is not exposed because SpiceSharp doesn't support it
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _mcpService.ExecuteTool("run_noise_analysis", arguments));
        
        Assert.Contains("Unknown tool: run_noise_analysis", exception.Message);
    }
}
