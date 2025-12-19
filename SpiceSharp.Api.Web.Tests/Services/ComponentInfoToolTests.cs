using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Api.Web.Models;
using SpiceSharp.Api.Web.Services;
using System.Text.Json;
using Xunit;

namespace SpiceSharp.Api.Web.Tests.Services;

/// <summary>
/// Tests for GetComponentInfo MCP tool - covers Phase 1A.2 from implementation plan
/// </summary>
public class ComponentInfoToolTests
{
    private readonly MCPService _mcpService;
    private readonly ICircuitManager _circuitManager;
    private readonly CircuitResultsCache _resultsCache;

    public ComponentInfoToolTests()
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
    public async Task GetComponentInfo_WithValidComponent_ReturnsInfo()
    {
        // Arrange
        var circuitId = "test_info";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Component info test");
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
            component = "R1"
        });

        // Act
        var result = await _mcpService.ExecuteTool("get_component_info", arguments);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("component_name", out _));
        Assert.True(resultData.TryGetProperty("component_type", out _));
        Assert.True(resultData.TryGetProperty("nodes", out _));
        Assert.True(resultData.TryGetProperty("value", out _));
        
        // Verify values
        Assert.Equal("R1", resultData.GetProperty("component_name").GetString());
        Assert.Equal("resistor", resultData.GetProperty("component_type").GetString());
        var value = resultData.GetProperty("value").GetDouble();
        Assert.True(Math.Abs(value - 1000.0) < 0.1, $"Expected value ≈ 1000.0, got {value}");
    }

    [Fact]
    public async Task GetComponentInfo_WithInvalidComponent_ThrowsException()
    {
        // Arrange
        var circuitId = "test_info";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Component info test");
        _circuitManager.SetActiveCircuit(circuitId);

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "R999" // Non-existent component
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _mcpService.ExecuteTool("get_component_info", arguments));
        
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetComponentInfo_IncludesModelInfo_WhenModelExists()
    {
        // Arrange
        var circuitId = "test_info_model";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Component info with model test");
        _circuitManager.SetActiveCircuit(circuitId);

        var modelService = new ModelService();
        modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "D1_MODEL",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-12 },
                { "N", 1.5 }
            }
        });

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "0" },
            Model = "D1_MODEL"
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            circuit_id = circuitId,
            component = "D1"
        });

        // Act
        var result = await _mcpService.ExecuteTool("get_component_info", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.True(resultData.TryGetProperty("model_name", out var modelName));
        Assert.Equal("D1_MODEL", modelName.GetString());
        Assert.True(resultData.TryGetProperty("model_type", out var modelType));
        Assert.Equal("diode", modelType.GetString());
        Assert.True(resultData.TryGetProperty("model_parameters", out var modelParams));
        Assert.True(modelParams.TryGetProperty("IS", out _));
        Assert.True(modelParams.TryGetProperty("N", out _));
    }

    [Fact]
    public async Task GetComponentInfo_WithoutCircuitId_UsesActiveCircuit()
    {
        // Arrange
        var circuitId = "test_active";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Active circuit test");
        _circuitManager.SetActiveCircuit(circuitId);

        var componentService = new ComponentService();
        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 2000.0
        });

        var arguments = JsonSerializer.SerializeToElement(new
        {
            component = "R1"
            // Note: circuit_id is omitted
        });

        // Act
        var result = await _mcpService.ExecuteTool("get_component_info", arguments);

        // Assert
        Assert.NotNull(result);
        var textContent = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textContent);
        
        var resultData = JsonSerializer.Deserialize<JsonElement>(textContent!.Text);
        Assert.Equal("R1", resultData.GetProperty("component_name").GetString());
        var value = resultData.GetProperty("value").GetDouble();
        Assert.True(Math.Abs(value - 2000.0) < 0.1, $"Expected value ≈ 2000.0, got {value}");
    }
}
