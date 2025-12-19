using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Integration tests for MCP tools - covers Test 3 scenarios from manual test suite
/// </summary>
public class IntegrationToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public IntegrationToolTests()
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
    public async Task CompleteWorkflow_CreateSweepPlotDelete_ExecutesSuccessfully()
    {
        // Test 3.1: Complete Workflow - Create, Sweep, Plot, Delete
        // Arrange & Act
        var circuitId = "workflow_test";
        
        // 1. Create circuit
        _circuitManager.CreateCircuit(circuitId, "Workflow test circuit");
        _circuitManager.SetActiveCircuit(circuitId);
        
        var componentService = new ComponentService();
        var circuit = _circuitManager.GetCircuit(circuitId);
        
        // 2. Add components (voltage divider)
        componentService.AddComponent(circuit!, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit!, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });
        componentService.AddComponent(circuit!, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        });

        // 3. Run parameter sweep
        var sweepArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1",
            parameter = "value",
            start = 100.0,
            stop = 10000.0,
            points = 10,
            scale = "linear",
            analysis_type = "operating_point",
            outputs = new[] { "v(out)" }
        });
        var sweepResult = await _mcpService.ExecuteTool("run_parameter_sweep", sweepArgs);
        Assert.NotNull(sweepResult);

        // 4. Plot results
        var plotArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            output_format = new[] { "text" }
        });
        var plotResult = await _mcpService.ExecuteTool("plot_results", plotArgs);
        Assert.NotNull(plotResult);

        // 5. Delete circuit
        var deleteArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId
        });
        await _mcpService.ExecuteTool("delete_circuit", deleteArgs);

        // 6. Verify deletion
        var circuitsAfter = _circuitManager.ListCircuits();
        Assert.DoesNotContain(circuitsAfter, c => c.Id == circuitId);
    }

    [Fact]
    public async Task MultipleCircuitsManagement_CreateListDelete_ManagesIndependently()
    {
        // Test 3.2: Multiple Circuits Management
        // Arrange & Act
        // 1. Create multiple circuits
        var circuit1 = _circuitManager.CreateCircuit("multi1", "Multi circuit 1");
        var circuit2 = _circuitManager.CreateCircuit("multi2", "Multi circuit 2");

        // 2. List circuits
        var circuits = _circuitManager.ListCircuits();
        Assert.Contains(circuits, c => c.Id == "multi1");
        Assert.Contains(circuits, c => c.Id == "multi2");

        // 3. Delete multi1
        var deleteArgs1 = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "multi1"
        });
        await _mcpService.ExecuteTool("delete_circuit", deleteArgs1);

        // 4. Verify multi1 is gone, multi2 still exists
        var circuitsAfter1 = _circuitManager.ListCircuits();
        Assert.DoesNotContain(circuitsAfter1, c => c.Id == "multi1");
        Assert.Contains(circuitsAfter1, c => c.Id == "multi2");

        // 5. Verify multi2 is now active (if it wasn't before)
        var activeCircuit = _circuitManager.GetActiveCircuit();
        Assert.NotNull(activeCircuit);
        Assert.Equal("multi2", activeCircuit.Id);

        // 6. Delete multi2
        var deleteArgs2 = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "multi2"
        });
        await _mcpService.ExecuteTool("delete_circuit", deleteArgs2);

        // 7. Verify both are gone
        var circuitsAfter2 = _circuitManager.ListCircuits();
        Assert.DoesNotContain(circuitsAfter2, c => c.Id == "multi1");
        Assert.DoesNotContain(circuitsAfter2, c => c.Id == "multi2");
    }
}
