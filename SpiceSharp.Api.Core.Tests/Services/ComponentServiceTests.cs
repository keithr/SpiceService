using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using SpiceSharp.Entities;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for ComponentService
/// </summary>
public class ComponentServiceTests
{
    private readonly ComponentService _componentService;
    private readonly CircuitManager _circuitManager;
    private readonly ModelService _modelService;

    public ComponentServiceTests()
    {
        _componentService = new ComponentService();
        _circuitManager = new CircuitManager();
        _modelService = new ModelService();
    }

    #region Dependent Source Tests

    [Fact]
    public void AddComponent_VCVS_WithValidInput_AddsToCircuit()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        };

        // Act
        var entity = _componentService.AddComponent(circuit, definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("E1", entity.Name);
        var retrieved = _componentService.GetComponent(circuit, "E1");
        Assert.NotNull(retrieved);
        Assert.Equal("E1", retrieved.Name);
    }

    [Fact]
    public void AddComponent_VCVS_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, definition));
        Assert.Contains("4", ex.Message);
    }

    [Fact]
    public void AddComponent_VCVS_WithoutGain_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, definition));
        Assert.Contains("gain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddComponent_VCCS_WithValidInput_AddsToCircuit()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "G1",
            ComponentType = "vccs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 0.001 } }
        };

        // Act
        var entity = _componentService.AddComponent(circuit, definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("G1", entity.Name);
    }

    [Fact]
    public void AddComponent_VCCS_WithoutGain_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "G1",
            ComponentType = "vccs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, definition));
        Assert.Contains("gain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddComponent_CCVS_WithValidInput_AddsToCircuit()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "H1",
            ComponentType = "ccvs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" },
            Parameters = new Dictionary<string, object> { { "gain", 100.0 } }
        };

        // Act
        var entity = _componentService.AddComponent(circuit, definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("H1", entity.Name);
    }

    [Fact]
    public void AddComponent_CCVS_WithInsufficientNodes_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "H1",
            ComponentType = "ccvs",
            Nodes = new List<string> { "out+", "out-", "ctrl+" },
            Parameters = new Dictionary<string, object> { { "gain", 100.0 } }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, definition));
        Assert.Contains("4", ex.Message);
    }

    [Fact]
    public void AddComponent_CCCS_WithValidInput_AddsToCircuit()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "F1",
            ComponentType = "cccs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" },
            Parameters = new Dictionary<string, object> { { "gain", 50.0 } }
        };

        // Act
        var entity = _componentService.AddComponent(circuit, definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("F1", entity.Name);
    }

    [Fact]
    public void AddComponent_CCCS_WithoutGain_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "F1",
            ComponentType = "cccs",
            Nodes = new List<string> { "out+", "out-", "ctrl+", "ctrl-" }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, definition));
        Assert.Contains("gain", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddComponent_DuplicateName_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition1 = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        };

        var definition2 = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out2+", "out2-", "in2+", "in2-" },
            Parameters = new Dictionary<string, object> { { "gain", 20.0 } }
        };

        // Act
        _componentService.AddComponent(circuit, definition1);

        // Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, definition2));
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetComponent_DependentSource_ReturnsComponent()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        };

        _componentService.AddComponent(circuit, definition);

        // Act
        var retrieved = _componentService.GetComponent(circuit, "E1");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("E1", retrieved.Name);
    }

    [Fact]
    public void ListComponents_WithDependentSources_IncludesAll()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "G1",
            ComponentType = "vccs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 0.001 } }
        });

        // Act
        var components = _componentService.ListComponents(circuit).ToList();

        // Assert
        Assert.Equal(2, components.Count);
        Assert.Contains(components, c => c.Name == "E1");
        Assert.Contains(components, c => c.Name == "G1");
    }

    [Fact]
    public void RemoveComponent_DependentSource_RemovesFromCircuit()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        var definition = new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out+", "out-", "in+", "in-" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        };

        _componentService.AddComponent(circuit, definition);
        Assert.NotNull(_componentService.GetComponent(circuit, "E1"));

        // Act
        var removed = _componentService.RemoveComponent(circuit, "E1");

        // Assert
        Assert.True(removed);
        Assert.Null(_componentService.GetComponent(circuit, "E1"));
    }

    #endregion

    #region Mutual Inductance Tests

    [Fact]
    public void AddComponent_MutualInductance_WithValidInductors_AddsToCircuit()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Create the two inductors first
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "L1",
            ComponentType = "inductor",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1e-3
        });
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "L2",
            ComponentType = "inductor",
            Nodes = new List<string> { "n3", "n4" },
            Value = 1e-3
        });

        var mutualDefinition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Nodes = new List<string>(),
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "inductor2", "L2" },
                { "coupling", 0.95 }
            }
        };

        // Act
        var entity = _componentService.AddComponent(circuit, mutualDefinition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("K1", entity.Name);
        Assert.NotNull(_componentService.GetComponent(circuit, "K1"));
    }

    [Fact]
    public void AddComponent_MutualInductance_WithNonExistentInductor1_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Create only L2, not L1
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "L2",
            ComponentType = "inductor",
            Nodes = new List<string> { "n3", "n4" },
            Value = 1e-3
        });

        var mutualDefinition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Nodes = new List<string>(),
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" }, // L1 doesn't exist
                { "inductor2", "L2" },
                { "coupling", 0.95 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, mutualDefinition));
        Assert.Contains("L1", ex.Message);
        Assert.Contains("does not exist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddComponent_MutualInductance_WithNonExistentInductor2_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        // Create only L1, not L2
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "L1",
            ComponentType = "inductor",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1e-3
        });

        var mutualDefinition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Nodes = new List<string>(),
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "inductor2", "L2" }, // L2 doesn't exist
                { "coupling", 0.95 }
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, mutualDefinition));
        Assert.Contains("L2", ex.Message);
        Assert.Contains("does not exist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddComponent_MutualInductance_WithInvalidCoupling_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_circuit", "Test circuit");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "L1",
            ComponentType = "inductor",
            Nodes = new List<string> { "n1", "n2" },
            Value = 1e-3
        });
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "L2",
            ComponentType = "inductor",
            Nodes = new List<string> { "n3", "n4" },
            Value = 1e-3
        });

        var mutualDefinition = new ComponentDefinition
        {
            Name = "K1",
            ComponentType = "mutual_inductance",
            Nodes = new List<string>(),
            Parameters = new Dictionary<string, object>
            {
                { "inductor1", "L1" },
                { "inductor2", "L2" },
                { "coupling", 1.5 } // Invalid: > 1
            }
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _componentService.AddComponent(circuit, mutualDefinition));
        Assert.Contains("coupling", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Switch Tests

    [Fact]
    public void AddComponent_VoltageSwitch_WithValidInput_AddsToCircuit()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("switch_test", "Switch test");
        
        // First create a switch model
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelType = "voltage_switch",
            ModelName = "SW_MODEL",
            Parameters = new Dictionary<string, double>
            {
                { "VT", 1.0 },
                { "VH", 0.5 },
                { "RON", 1.0 },
                { "ROFF", 1e6 }
            }
        });

        var definition = new ComponentDefinition
        {
            Name = "S1",
            ComponentType = "voltage_switch",
            Nodes = new List<string> { "out+", "out-" },
            Parameters = new Dictionary<string, object>
            {
                { "controlNodes", new[] { "ctrl+", "ctrl-" } },
                { "model", "SW_MODEL" }
            }
        };

        // Act
        var entity = _componentService.AddComponent(circuit, definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("S1", entity.Name);
        Assert.NotNull(_componentService.GetComponent(circuit, "S1"));
    }

    [Fact]
    public void AddComponent_CurrentSwitch_WithValidInput_AddsToCircuit()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("switch_test2", "Current switch test");
        
        // First create a switch model
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelType = "current_switch",
            ModelName = "CSW_MODEL",
            Parameters = new Dictionary<string, double>
            {
                { "IT", 0.001 },
                { "IH", 0.0005 },
                { "RON", 1.0 },
                { "ROFF", 1e6 }
            }
        });

        // Create a control voltage source
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V_CTRL",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "ctrl+", "ctrl-" },
            Value = 0.0
        });

        var definition = new ComponentDefinition
        {
            Name = "W1",
            ComponentType = "current_switch",
            Nodes = new List<string> { "out+", "out-" },
            Parameters = new Dictionary<string, object>
            {
                { "controlSource", "V_CTRL" },
                { "model", "CSW_MODEL" }
            }
        };

        // Act
        var entity = _componentService.AddComponent(circuit, definition);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal("W1", entity.Name);
        Assert.NotNull(_componentService.GetComponent(circuit, "W1"));
    }

    #endregion
}

