using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for DeleteCircuit MCP tool - covers Test 1 scenarios from manual test suite
/// </summary>
public class DeleteCircuitToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public DeleteCircuitToolTests()
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
    public async Task DeleteCircuit_WithValidCircuit_DeletesSuccessfully()
    {
        // Test 1.1: Basic Delete
        // Arrange
        var circuitId = "test_delete1";
        _circuitManager.CreateCircuit(circuitId, "Test circuit for deletion");
        
        // Verify it exists
        var circuitsBefore = _circuitManager.ListCircuits();
        Assert.Contains(circuitsBefore, c => c.Id == circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId
        });

        // Act
        var result = await _mcpService.ExecuteTool("delete_circuit", arguments);

        // Assert
        Assert.NotNull(result);
        var circuitsAfter = _circuitManager.ListCircuits();
        Assert.DoesNotContain(circuitsAfter, c => c.Id == circuitId);
    }

    [Fact]
    public async Task DeleteCircuit_WithActiveCircuit_ActivatesAnotherCircuit()
    {
        // Test 1.2: Delete Active Circuit
        // Arrange
        var circuit1 = _circuitManager.CreateCircuit("test1", "Test circuit 1");
        var circuit2 = _circuitManager.CreateCircuit("test2", "Test circuit 2");
        _circuitManager.SetActiveCircuit("test1");

        // Verify test1 is active
        var activeBefore = _circuitManager.GetActiveCircuit();
        Assert.NotNull(activeBefore);
        Assert.Equal("test1", activeBefore.Id);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "test1"
        });

        // Act
        await _mcpService.ExecuteTool("delete_circuit", arguments);

        // Assert
        var activeAfter = _circuitManager.GetActiveCircuit();
        Assert.NotNull(activeAfter);
        Assert.Equal("test2", activeAfter.Id); // test2 should now be active
    }

    [Fact]
    public async Task DeleteCircuit_WithNonExistentCircuit_ThrowsException()
    {
        // Test 1.3: Delete Non-Existent Circuit
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = "nonexistent"
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("delete_circuit", arguments));
        
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteCircuit_WithCachedResults_ClearsCache()
    {
        // Test 1.4: Delete Circuit with Cached Results
        // Arrange
        var circuitId = "test_cache";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit with cache");
        _circuitManager.SetActiveCircuit(circuitId);

        // Add components and run analysis to create cached results
        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "in" },
            Value = 5.0
        });

        var operatingPointService = new OperatingPointService();
        var opResult = operatingPointService.RunOperatingPointAnalysis(circuit);
        
        // Convert OperatingPointResult to CachedAnalysisResult
        var cachedResult = new CachedAnalysisResult
        {
            AnalysisType = "operating_point",
            OperatingPointData = opResult.NodeVoltages.ToDictionary(kvp => $"v({kvp.Key})", kvp => kvp.Value)
        };
        foreach (var current in opResult.BranchCurrents)
        {
            cachedResult.OperatingPointData[$"i({current.Key})"] = current.Value;
        }
        _resultsCache.Store(circuitId, cachedResult);

        // Verify cache has results
        var cachedBefore = _resultsCache.Get(circuitId);
        Assert.NotNull(cachedBefore);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId
        });

        // Act
        await _mcpService.ExecuteTool("delete_circuit", arguments);

        // Assert
        var cachedAfter = _resultsCache.Get(circuitId);
        Assert.Null(cachedAfter); // Cache should be cleared
    }

    [Fact]
    public async Task DeleteCircuit_WithEmptyCircuitId_ThrowsException()
    {
        // Test 4.1: Delete Circuit with Invalid ID
        // Arrange
        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = ""
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("delete_circuit", arguments));
        
        Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
