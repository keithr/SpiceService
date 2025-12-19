using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for ModifyComponent MCP tool - covers Phase 1A.3 from implementation plan
/// </summary>
public class ModifyComponentToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public ModifyComponentToolTests()
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
    public async Task ModifyComponent_WithValidParameters_UpdatesComponent()
    {
        // Arrange
        var circuitId = "test_modify";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Modify component test");
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
            parameters = new Dictionary<string, object>
            {
                { "value", 2000.0 }
            }
        });

        // Act
        var result = await _mcpService.ExecuteTool("modify_component", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        
        // Verify component was updated by getting its info
        var infoArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R1"
        });
        var infoResult = await _mcpService.ExecuteTool("get_component_info", infoArgs);
        var textContent = infoResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var infoData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        var updatedValue = infoData.GetProperty("value").GetDouble();
        Assert.True(Math.Abs(updatedValue - 2000.0) < 0.1, $"Expected value ≈ 2000.0, got {updatedValue}");
    }

    [Fact]
    public async Task ModifyComponent_WithInvalidComponent_ThrowsException()
    {
        // Arrange
        var circuitId = "test_modify";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Modify component test");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R999", // Non-existent component
            parameters = new Dictionary<string, object>
            {
                { "value", 2000.0 }
            }
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("modify_component", arguments));
        
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModifyComponent_WithVoltageSource_UpdatesDCValue()
    {
        // Arrange
        var circuitId = "test_modify_v";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Modify voltage source test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "V1",
            parameters = new Dictionary<string, object>
            {
                { "value", 10.0 }
            }
        });

        // Act
        var result = await _mcpService.ExecuteTool("modify_component", arguments);

        // Assert
        Assert.NotNull(result);
        
        // Verify component was updated
        var infoArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "V1"
        });
        var infoResult = await _mcpService.ExecuteTool("get_component_info", infoArgs);
        var textContent = infoResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var infoData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        var updatedValue = infoData.GetProperty("value").GetDouble();
        Assert.True(Math.Abs(updatedValue - 10.0) < 0.1, $"Expected value ≈ 10.0, got {updatedValue}");
    }

    [Fact]
    public async Task ModifyComponent_WithACParameter_UpdatesACValue()
    {
        // Arrange
        var circuitId = "test_modify_ac";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Modify AC parameter test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 5.0,
            Parameters = new Dictionary<string, object>
            {
                { "ac", 1.0 }
            }
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "V1",
            parameters = new Dictionary<string, object>
            {
                { "ac", 2.0 }
            }
        });

        // Act
        var result = await _mcpService.ExecuteTool("modify_component", arguments);

        // Assert
        Assert.NotNull(result);
        
        // Verify AC parameter was updated
        var infoArgs = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "V1"
        });
        var infoResult = await _mcpService.ExecuteTool("get_component_info", infoArgs);
        var textContent = infoResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var infoData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        var parameters = infoData.GetProperty("parameters");
        Assert.True(parameters.TryGetProperty("ac", out var acValue));
        Assert.True(Math.Abs(acValue.GetDouble() - 2.0) < 0.1, $"Expected ac ≈ 2.0, got {acValue.GetDouble()}");
    }

    [Fact]
    public async Task ModifyComponent_WithoutCircuitId_UsesActiveCircuit()
    {
        // Arrange
        var circuitId = "test_modify_active";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Active circuit modify test");
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
            component = "R1",
            parameters = new Dictionary<string, object>
            {
                { "value", 3000.0 }
            }
            // Note: circuit_id is omitted
        });

        // Act
        var result = await _mcpService.ExecuteTool("modify_component", arguments);

        // Assert
        Assert.NotNull(result);
        
        // Verify component was updated
        var infoArgs = JsonSerializer.SerializeToElement(new
        {
            component = "R1"
        });
        var infoResult = await _mcpService.ExecuteTool("get_component_info", infoArgs);
        var textContent = infoResult.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        var infoData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        var updatedValue = infoData.GetProperty("value").GetDouble();
        Assert.True(Math.Abs(updatedValue - 3000.0) < 0.1, $"Expected value ≈ 3000.0, got {updatedValue}");
    }
}
