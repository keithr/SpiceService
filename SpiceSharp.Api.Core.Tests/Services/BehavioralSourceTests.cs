using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for behavioral voltage and current sources with expression support.
/// Phase 2 (TDD RED): These tests are expected to FAIL until implementation in Phase 3.
/// </summary>
public class BehavioralSourceTests
{
    private readonly ComponentFactory _factory;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;

    public BehavioralSourceTests()
    {
        _factory = new ComponentFactory();
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
    }

    #region Behavioral Voltage Source Tests (Tests 1-5)

    /// <summary>
    /// Test 1: Create behavioral voltage source with simple expression.
    /// Expected: FAIL (component type not implemented yet)
    /// </summary>
    [Fact]
    public void CreateBehavioralVoltageSource_WithSimpleExpression_Success()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "B1",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(input) * 2.0" }
            }
        };

        // Act
        var component = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(component);
        Assert.Equal("B1", component.Name);
        // Note: This test will FAIL until behavioral_voltage_source is implemented
    }

    /// <summary>
    /// Test 2: Create behavioral voltage source with complex expression.
    /// Expected: FAIL (component type not implemented yet)
    /// </summary>
    [Fact]
    public void CreateBehavioralVoltageSource_WithComplexExpression_Success()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "B2",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "out", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(input) * 5.1 + 2.0" }
            }
        };

        // Act
        var component = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(component);
        Assert.Equal("B2", component.Name);
    }

    /// <summary>
    /// Test 3: Create behavioral voltage source with multiple node references.
    /// Expected: FAIL (component type not implemented yet)
    /// </summary>
    [Fact]
    public void CreateBehavioralVoltageSource_WithNodeReferences_Success()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "B3",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "diff", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(a) - V(b)" }
            }
        };

        // Act
        var component = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(component);
        Assert.Equal("B3", component.Name);
    }

    /// <summary>
    /// Test 4: Behavioral voltage source without expression parameter should throw.
    /// Expected: FAIL (exception not thrown yet because type not implemented)
    /// </summary>
    [Fact]
    public void CreateBehavioralVoltageSource_MissingExpression_ThrowsException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "B4",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>() // No expression!
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("expression", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test 5: Behavioral voltage source with empty expression should throw.
    /// Expected: FAIL (exception not thrown yet because type not implemented)
    /// </summary>
    [Fact]
    public void CreateBehavioralVoltageSource_EmptyExpression_ThrowsException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "B5",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "" } // Empty expression!
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("expression", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Behavioral Current Source Tests (Tests 6-8)

    /// <summary>
    /// Test 6: Create behavioral current source with simple expression.
    /// Expected: FAIL (component type not implemented yet)
    /// </summary>
    [Fact]
    public void CreateBehavioralCurrentSource_WithSimpleExpression_Success()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "B6",
            ComponentType = "behavioral_current_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "I(Vsense) * 10.0" }
            }
        };

        // Act
        var component = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(component);
        Assert.Equal("B6", component.Name);
    }

    /// <summary>
    /// Test 7: Create behavioral current source with voltage-based expression.
    /// Expected: FAIL (component type not implemented yet)
    /// </summary>
    [Fact]
    public void CreateBehavioralCurrentSource_WithComplexExpression_Success()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "B7",
            ComponentType = "behavioral_current_source",
            Nodes = new List<string> { "out", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(voltage) / 1000.0" }
            }
        };

        // Act
        var component = _factory.CreateComponent(definition);

        // Assert
        Assert.NotNull(component);
        Assert.Equal("B7", component.Name);
    }

    /// <summary>
    /// Test 8: Behavioral current source without expression should throw.
    /// Expected: FAIL (exception not thrown yet because type not implemented)
    /// </summary>
    [Fact]
    public void CreateBehavioralCurrentSource_MissingExpression_ThrowsException()
    {
        // Arrange
        var definition = new ComponentDefinition
        {
            Name = "B8",
            ComponentType = "behavioral_current_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>() // No expression!
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateComponent(definition));
        Assert.Contains("expression", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Integration Tests (Tests 9-11)

    /// <summary>
    /// Test 9: Behavioral voltage source in DC analysis produces correct output.
    /// Expected: FAIL (component type not implemented yet)
    /// </summary>
    [Fact]
    public void BehavioralVoltageSource_DCAnalysis_CorrectOutput()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_bvs_dc", "Test BVS DC Analysis");
        
        // Add input voltage source
        var vInput = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "input", "0" },
            Value = 1.0 // 1V DC
        };
        _componentService.AddComponent(circuit, vInput);

        // Add behavioral voltage source (output = input * 2.5)
        var bvs = new ComponentDefinition
        {
            Name = "B1",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(input) * 2.5" }
            }
        };
        _componentService.AddComponent(circuit, bvs);

        // Add load resistor
        var rLoad = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "output", "0" },
            Value = 10000.0
        };
        _componentService.AddComponent(circuit, rLoad);

        // Act
        var dcAnalysisService = new DCAnalysisService();
        var result = dcAnalysisService.RunDCAnalysis(circuit, "V1", 0, 2, 0.5, new List<string> { "v(input)", "v(output)" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.NotNull(result.Results);
        
        // Verify output = input * 2.5 for each sweep point
        var inputs = result.Results["v(input)"];
        var outputs = result.Results["v(output)"];
        
        for (int i = 0; i < inputs.Count; i++)
        {
            var expectedOutput = inputs[i] * 2.5;
            Assert.Equal(expectedOutput, outputs[i], precision: 3);
        }
    }

    /// <summary>
    /// Test 10: Behavioral voltage source with multi-node expression.
    /// Expected: FAIL (component type not implemented yet)
    /// </summary>
    [Fact]
    public void BehavioralVoltageSource_MultiNodeExpression_CorrectOutput()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_bvs_multi", "Test BVS Multi-Node");
        
        // Add two voltage sources
        var vA = new ComponentDefinition
        {
            Name = "VA",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "a", "0" },
            Value = 3.0
        };
        _componentService.AddComponent(circuit, vA);

        var vB = new ComponentDefinition
        {
            Name = "VB",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "b", "0" },
            Value = 1.0
        };
        _componentService.AddComponent(circuit, vB);

        // Add behavioral voltage source (output = a - b * 2.0)
        var bvs = new ComponentDefinition
        {
            Name = "B1",
            ComponentType = "behavioral_voltage_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(a) - V(b) * 2.0" }
            }
        };
        _componentService.AddComponent(circuit, bvs);

        // Add load
        var rLoad = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "output", "0" },
            Value = 10000.0
        };
        _componentService.AddComponent(circuit, rLoad);

        // Act
        var opService = new OperatingPointService();
        var result = opService.RunOperatingPointAnalysis(circuit, includePower: false);

        // Assert
        Assert.Equal("Success", result.Status);
        // Expected: 3.0 - (1.0 * 2.0) = 1.0V
        Assert.Equal(1.0, result.NodeVoltages["output"], precision: 3);
    }

    /// <summary>
    /// Test 11: Behavioral current source in DC analysis.
    /// Expected: FAIL (component type not implemented yet)
    /// </summary>
    [Fact]
    public void BehavioralCurrentSource_DCAnalysis_CorrectOutput()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test_bcs_dc", "Test BCS DC Analysis");
        
        // Add voltage source to create reference voltage
        var vRef = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "ref", "0" },
            Value = 2.0 // 2V
        };
        _componentService.AddComponent(circuit, vRef);

        // Add sense resistor for reference
        var rSense = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "ref", "0" },
            Value = 1000.0
        };
        _componentService.AddComponent(circuit, rSense);

        // Add behavioral current source (current = V(ref) / 1000)
        var bcs = new ComponentDefinition
        {
            Name = "B1",
            ComponentType = "behavioral_current_source",
            Nodes = new List<string> { "output", "0" },
            Parameters = new Dictionary<string, object>
            {
                { "expression", "V(ref) / 1000.0" }
            }
        };
        _componentService.AddComponent(circuit, bcs);

        // Add load resistor (1kΩ to verify current)
        var rLoad = new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "output", "0" },
            Value = 1000.0
        };
        _componentService.AddComponent(circuit, rLoad);

        // Act
        var opService = new OperatingPointService();
        var result = opService.RunOperatingPointAnalysis(circuit, includePower: false);

        // Assert
        Assert.Equal("Success", result.Status);
        // Expected current: 2V / 1000Ω = 0.002A = 2mA
        // Voltage across 1kΩ load: 2mA * 1000Ω = 2V
        // With the expression negation fix, the sign should now be correct
        Assert.Equal(2.0, result.NodeVoltages["output"], precision: 3);
    }

    #endregion
}
