using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Error handling tests for MCP tools - covers Test 4 scenarios from manual test suite
/// </summary>
public class ErrorHandlingToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public ErrorHandlingToolTests()
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
            config);
    }

    [Fact]
    public async Task ParameterSweep_WithStartGreaterThanStop_ThrowsException()
    {
        // Test 4.2: Parameter Sweep with Invalid Step Calculation
        // Arrange
        var circuitId = "test1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Invalid step test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            start = 10000.0,
            stop = 100.0, // start > stop
            points = 10,
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("run_parameter_sweep", arguments));
        
        Assert.Contains("start", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stop", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParameterSweep_WithSinglePoint_ThrowsException()
    {
        // Test 4.3: Parameter Sweep with Single Point
        // Arrange
        var circuitId = "test1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Single point test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            start = 1000.0,
            stop = 1000.0,
            points = 1, // Only one point
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("run_parameter_sweep", arguments));
        
        Assert.Contains("points", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParameterSweep_WithLogScaleAndNegativeValues_ThrowsException()
    {
        // Test 4.4: Parameter Sweep with Log Scale and Negative Values
        // Arrange
        var circuitId = "test1";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Log scale negative test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            start = -100.0, // Negative value
            stop = 1000.0,
            points = 10,
            scale = "log", // Log scale requires positive values
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("run_parameter_sweep", arguments));
        
        Assert.Contains("log", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("positive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
