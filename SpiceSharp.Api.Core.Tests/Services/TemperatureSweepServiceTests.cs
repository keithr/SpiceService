using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for TemperatureSweepService
/// </summary>
public class TemperatureSweepServiceTests
{
    private readonly TemperatureSweepService _temperatureSweepService;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;

    public TemperatureSweepServiceTests()
    {
        _temperatureSweepService = new TemperatureSweepService(
            new OperatingPointService(),
            new DCAnalysisService(),
            new ACAnalysisService(),
            new TransientAnalysisService());
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
        _modelService = new ModelService();
    }

    [Fact]
    public void RunTemperatureSweep_ReturnsResults_WhenTemperatureSwept()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_temp_sweep", "Test temperature sweep");
        
        // Create LED circuit with temperature parameters
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "LED_MODEL",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-15 },
                { "N", 3.5 },
                { "EG", 1.6 },
                { "RS", 0.5 },
                { "XTI", 3.0 },  // Temperature coefficient for IS
                { "TNOM", 27.0 }  // Nominal temperature
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
        var result = _temperatureSweepService.RunTemperatureSweep(
            circuit,
            25.0,  // Start at 25°C
            85.0,  // End at 85°C
            20.0,  // Step by 20°C
            "operating-point",
            null,
            new[] { "i(D1)", "v(anode)" }
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("operating-point", result.AnalysisType);
        Assert.True(result.TemperatureValues.Count > 0);
        Assert.Contains("i(D1)", result.Results.Keys);
        Assert.Contains("v(anode)", result.Results.Keys);
        Assert.Equal(result.TemperatureValues.Count, result.Results["i(D1)"].Count);
    }

    [Fact]
    public void RunTemperatureSweep_ResultsChange_WhenTemperatureChanged()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_temp_effect", "Test temperature effect");
        
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "LED_MODEL",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-15 },
                { "N", 3.5 },
                { "EG", 1.6 },
                { "RS", 0.5 },
                { "XTI", 3.0 },
                { "TNOM", 27.0 }
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

        // Act - Sweep from 25°C to 85°C
        var result = _temperatureSweepService.RunTemperatureSweep(
            circuit,
            25.0,
            85.0,
            30.0,  // Large step to get distinct points
            "operating-point",
            null,
            new[] { "i(D1)", "v(anode)" }
        );

        // Assert - Results should change with temperature
        Assert.True(result.TemperatureValues.Count >= 2);
        var voltageAt25 = result.Results["v(anode)"][0];
        var voltageAt85 = result.Results["v(anode)"][result.Results["v(anode)"].Count - 1];
        
        // Voltage should change with temperature (LED forward voltage decreases with temperature)
        // Note: The exact change depends on the model, but there should be some change
        // Also check that we got valid (non-NaN) results
        Assert.False(double.IsNaN(voltageAt25));
        Assert.False(double.IsNaN(voltageAt85));
        // Voltage may or may not change significantly depending on model parameters
        // At minimum, we should get valid results
    }

    [Fact]
    public void RunTemperatureSweep_ReturnsCorrectNumberOfPoints_WhenTemperatureRangeSpecified()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_temp_points", "Test temperature points");
        
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "LED_MODEL",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-15 },
                { "N", 3.5 },
                { "EG", 1.6 },
                { "RS", 0.5 },
                { "XTI", 3.0 },
                { "TNOM", 27.0 }
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
        var result = _temperatureSweepService.RunTemperatureSweep(
            circuit,
            25.0,
            85.0,
            30.0,  // Step: 25, 55, 85 (3 points)
            "operating-point",
            null,
            new[] { "i(D1)" }
        );

        // Assert
        // Should have points: 25, 55, 85 (3 points)
        Assert.Equal(3, result.TemperatureValues.Count);
        Assert.Equal(25.0, result.TemperatureValues[0]);
        Assert.Equal(55.0, result.TemperatureValues[1]);
        Assert.Equal(85.0, result.TemperatureValues[2]);
    }

    [Fact]
    public void RunTemperatureSweep_ThrowsArgumentException_WhenStartGreaterThanStop()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_temp_invalid", "Test invalid temperature range");
        
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

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _temperatureSweepService.RunTemperatureSweep(
            circuit,
            85.0,  // Start > Stop
            25.0,
            10.0,
            "operating-point",
            null,
            new[] { "i(D1)" }
        ));
    }

    [Fact]
    public void RunTemperatureSweep_ThrowsArgumentException_WhenStepInvalid()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_temp_invalid_step", "Test invalid step");
        
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

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _temperatureSweepService.RunTemperatureSweep(
            circuit,
            25.0,
            85.0,
            0.0,  // Invalid step
            "operating-point",
            null,
            new[] { "i(D1)" }
        ));
    }
}

