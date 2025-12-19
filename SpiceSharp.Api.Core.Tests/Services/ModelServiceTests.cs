using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Entities;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for ModelService
/// </summary>
public class ModelServiceTests
{
    private readonly ModelService _modelService;
    private readonly CircuitManager _circuitManager;

    public ModelServiceTests()
    {
        _modelService = new ModelService();
        _circuitManager = new CircuitManager();
    }

    [Fact]
    public void DefineModel_WithValidDiode_CreatesModel()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test1", "Test circuit");
        var definition = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "1N4148",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-12 },
                { "N", 1.5 }
            }
        };

        // Act
        var model = _modelService.DefineModel(circuit, definition);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("1N4148", model.Name);
    }

    [Fact]
    public void DefineModel_WithAdvancedDiodeParameters_CreatesModel()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_advanced", "Test advanced diode");
        
        // Test parameters that SpiceSharp DiodeModel actually supports
        // Based on testing, SpiceSharp supports: IS, N, EG, RS, CJO, VJ, M, TT, FC, BV, IBV, XTI, TNOM
        // Note: Some parameters like IKF, KF, AF, NBV, TRS1, TRS2, TBV1, TBV2, AREA, PJ may not be supported
        var definition = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "LED_ADVANCED",
            Parameters = new Dictionary<string, double>
            {
                // Basic parameters (known to work)
                { "IS", 1e-15 },
                { "N", 3.5 },
                { "EG", 1.6 },
                { "RS", 0.5 },
                // AC/High-frequency parameters (tested - work)
                { "CJO", 50e-12 },
                { "VJ", 0.7 },
                { "M", 0.33 },
                { "TT", 5e-9 },
                { "FC", 0.5 },
                // Breakdown parameters (tested - work)
                { "BV", 5.0 },
                { "IBV", 10e-6 },
                // Temperature parameters (tested - work)
                { "XTI", 3.0 },
                { "TNOM", 27.0 }
            }
        };

        // Act
        var model = _modelService.DefineModel(circuit, definition);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("LED_ADVANCED", model.Name);
    }

    [Fact]
    public void DefineModel_RejectsRSHParameter_WithHelpfulErrorMessage()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_rsh", "Test RSH parameter");
        var definition = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "LED_WITH_RSH",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-15 },
                { "N", 3.5 },
                { "EG", 1.6 },
                { "RS", 0.5 },
                { "RSH", 1e6 } // Shunt resistance parameter - NOT SUPPORTED by SpiceSharp
            }
        };

        // Act & Assert
        // RSH is not supported - validation should throw ArgumentException with helpful message
        var exception = Assert.Throws<ArgumentException>(() => 
            _modelService.DefineModel(circuit, definition));
        
        // Verify the exception mentions RSH and workaround
        Assert.Contains("RSH", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parallel resistor", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefineModel_RejectsIKFParameter_WithHelpfulErrorMessage()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_ikf", "Test IKF parameter");
        var definition = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "LED_WITH_IKF",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-15 },
                { "N", 3.5 },
                { "IKF", 0.02 } // High-injection knee current - NOT SUPPORTED
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            _modelService.DefineModel(circuit, definition));
        
        Assert.Contains("IKF", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefineModel_RejectsUnknownParameter_WithHelpfulErrorMessage()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_unknown", "Test unknown parameter");
        var definition = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "LED_UNKNOWN",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-15 },
                { "UNKNOWN_PARAM", 1.0 } // Unknown parameter
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            _modelService.DefineModel(circuit, definition));
        
        Assert.Contains("UNKNOWN_PARAM", exception.Message);
        Assert.Contains("Unknown diode parameter", exception.Message);
        Assert.Contains("/api/models/types", exception.Message);
    }

    [Fact]
    public void DefineModel_AcceptsAllSupportedParameters()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_all_params", "Test all supported parameters");
        var definition = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "LED_COMPLETE",
            Parameters = new Dictionary<string, double>
            {
                // All 13 supported parameters
                { "IS", 1e-15 },
                { "N", 3.5 },
                { "EG", 1.6 },
                { "RS", 0.5 },
                { "CJO", 50e-12 },
                { "VJ", 0.7 },
                { "M", 0.33 },
                { "TT", 5e-9 },
                { "FC", 0.5 },
                { "BV", 5.0 },
                { "IBV", 10e-6 },
                { "XTI", 3.0 },
                { "TNOM", 27.0 }
            }
        };

        // Act
        var model = _modelService.DefineModel(circuit, definition);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("LED_COMPLETE", model.Name);
    }

    [Fact]
    public void DefineModel_WithValidBJT_CreatesModel()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test1", "Test circuit");
        var definition = new ModelDefinition
        {
            ModelType = "bjt_npn",
            ModelName = "2N2222",
            Parameters = new Dictionary<string, double>
            {
                { "BF", 100 },
                { "VAF", 50 }
            }
        };

        // Act
        var model = _modelService.DefineModel(circuit, definition);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("2N2222", model.Name);
    }

    [Fact]
    public void DefineModel_WithDuplicateName_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test1", "Test circuit");
        var definition1 = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "1N4148",
            Parameters = new Dictionary<string, double> { { "IS", 1e-12 } }
        };

        _modelService.DefineModel(circuit, definition1);

        var definition2 = new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "1N4148",
            Parameters = new Dictionary<string, double> { { "IS", 2e-12 } }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _modelService.DefineModel(circuit, definition2));
    }

    [Fact]
    public void GetModel_WithExistingModel_ReturnsModel()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test1", "Test circuit");
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "1N4148",
            Parameters = new Dictionary<string, double> { { "IS", 1e-12 } }
        });

        // Act
        var model = _modelService.GetModel(circuit, "1N4148");

        // Assert
        Assert.NotNull(model);
        Assert.Equal("1N4148", model!.Name);
    }

    [Fact]
    public void GetModel_WithNonExistentModel_ReturnsNull()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test1", "Test circuit");

        // Act
        var model = _modelService.GetModel(circuit, "NonexistentModel");

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public void ListModels_WithMultipleModels_ReturnsAllModels()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test1", "Test circuit");
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelType = "diode",
            ModelName = "1N4148",
            Parameters = new Dictionary<string, double> { { "IS", 1e-12 } }
        });

        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelType = "bjt_npn",
            ModelName = "2N2222",
            Parameters = new Dictionary<string, double> { { "BF", 100 } }
        });

        // Act
        var models = _modelService.ListModels(circuit).ToList();

        // Assert
        Assert.Equal(2, models.Count);
        Assert.Contains(models, m => m.Name == "1N4148");
        Assert.Contains(models, m => m.Name == "2N2222");
    }

    [Fact]
    public void DefineModel_WithValidVoltageSwitchModel_CreatesModel()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("switch_test", "Switch model test");
        var definition = new ModelDefinition
        {
            ModelType = "voltage_switch",
            ModelName = "SW_MODEL",
            Parameters = new Dictionary<string, double>
            {
                { "VT", 1.0 },  // Threshold voltage
                { "VH", 0.5 },  // Hysteresis voltage
                { "RON", 1.0 }, // On resistance
                { "ROFF", 1e6 } // Off resistance
            }
        };

        // Act
        var model = _modelService.DefineModel(circuit, definition);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("SW_MODEL", model.Name);
    }

    [Fact]
    public void DefineModel_WithValidCurrentSwitchModel_CreatesModel()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("switch_test2", "Current switch model test");
        var definition = new ModelDefinition
        {
            ModelType = "current_switch",
            ModelName = "CSW_MODEL",
            Parameters = new Dictionary<string, double>
            {
                { "IT", 0.001 }, // Threshold current
                { "IH", 0.0005 }, // Hysteresis current
                { "RON", 1.0 },   // On resistance
                { "ROFF", 1e6 }   // Off resistance
            }
        };

        // Act
        var model = _modelService.DefineModel(circuit, definition);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("CSW_MODEL", model.Name);
    }
}
