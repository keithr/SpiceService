using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for ParameterSweepService
/// </summary>
public class ParameterSweepServiceTests
{
    private readonly ParameterSweepService _parameterSweepService;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;
    private readonly OperatingPointService _operatingPointService;

    public ParameterSweepServiceTests()
    {
        _parameterSweepService = new ParameterSweepService(
            new OperatingPointService(),
            new DCAnalysisService(),
            new ACAnalysisService(),
            new TransientAnalysisService());
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
        _modelService = new ModelService();
        _operatingPointService = new OperatingPointService();
    }

    [Fact]
    public void ParseParameterPath_ReturnsComponentPath_WhenComponentProperty()
    {
        // Arrange
        var path = "R1.value";

        // Act
        var result = ParameterSweepService.ParseParameterPath(path);

        // Assert
        Assert.Equal("R1", result.ComponentName);
        Assert.Equal("value", result.PropertyName);
        Assert.Null(result.ModelName);
        Assert.Null(result.ParameterName);
        Assert.True(result.IsComponentPath);
        Assert.False(result.IsModelPath);
    }

    [Fact]
    public void ParseParameterPath_ReturnsModelPath_WhenModelParameter()
    {
        // Arrange
        var path = "LED_MODEL.IS";

        // Act
        var result = ParameterSweepService.ParseParameterPath(path);

        // Assert
        Assert.Null(result.ComponentName);
        Assert.Null(result.PropertyName);
        Assert.Equal("LED_MODEL", result.ModelName);
        Assert.Equal("IS", result.ParameterName);
        Assert.False(result.IsComponentPath);
        Assert.True(result.IsModelPath);
    }

    [Fact]
    public void ParseParameterPath_ThrowsArgumentException_WhenInvalidFormat()
    {
        // Arrange
        var path = "InvalidPath";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ParameterSweepService.ParseParameterPath(path));
    }

    [Fact]
    public void RunParameterSweep_ReturnsResults_WhenComponentSwept()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_sweep", "Test parameter sweep");
        
        // Create a simple circuit: V1 -> R1 -> 0
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "anode", "0" },
            Value = 5.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "anode", "0" },
            Value = 100.0
        });

        // Act
        var result = _parameterSweepService.RunParameterSweep(
            circuit,
            "R1.value",
            100.0,
            200.0,
            50.0,
            "operating-point",
            null,
            new[] { "v(anode)" }
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("R1.value", result.ParameterPath);
        Assert.Equal("operating-point", result.AnalysisType);
        Assert.True(result.ParameterValues.Count > 0);
        Assert.Contains("v(anode)", result.Results.Keys);
    }

    [Fact]
    public void RunParameterSweep_ReturnsCorrectNumberOfPoints_WhenSweepRangeSpecified()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_sweep_points", "Test sweep points");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "anode", "0" },
            Value = 5.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "anode", "0" },
            Value = 100.0
        });

        // Act
        var result = _parameterSweepService.RunParameterSweep(
            circuit,
            "R1.value",
            100.0,
            200.0,
            50.0,
            "operating-point",
            null,
            new[] { "v(anode)" }
        );

        // Assert
        // Should have points: 100, 150, 200 (3 points)
        Assert.Equal(3, result.ParameterValues.Count);
        Assert.Equal(100.0, result.ParameterValues[0]);
        Assert.Equal(150.0, result.ParameterValues[1]);
        Assert.Equal(200.0, result.ParameterValues[2]);
    }

    [Fact]
    public void RunParameterSweep_ReturnsResults_WhenModelParameterSwept()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_model_sweep", "Test model parameter sweep");
        
        // Create LED circuit
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "LED_MODEL",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-15 },
                { "N", 3.5 },
                { "EG", 1.6 },
                { "RS", 0.5 }
            }
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "anode", "0" },
            Value = 3.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "0" },
            Model = "LED_MODEL"
        });

        // Act
        var result = _parameterSweepService.RunParameterSweep(
            circuit,
            "LED_MODEL.IS",
            1e-15,
            1e-14,
            1e-15,
            "operating-point",
            null,
            new[] { "i(D1)" }
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("LED_MODEL.IS", result.ParameterPath);
        Assert.True(result.ParameterValues.Count > 0);
        Assert.Contains("i(D1)", result.Results.Keys);
    }
}

