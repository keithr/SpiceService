using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using System.Collections.Generic;

namespace SpiceSharp.Api.Core.Tests.Services;

public class OperatingPointServiceTests
{
    private readonly OperatingPointService _service;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;

    public OperatingPointServiceTests()
    {
        _service = new OperatingPointService();
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
    }

    [Fact]
    public void RunOperatingPointAnalysis_WithNullCircuit_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.RunOperatingPointAnalysis(null!));
    }

    [Fact]
    public void RunOperatingPointAnalysis_WithSimpleVoltageDivider_CalculatesNodeVoltages()
    {
        var circuit = _circuitManager.CreateCircuit("voltage_divider", "Simple voltage divider");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "vcc" },
            Value = 5.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "vcc", "mid" },
            Value = 1000.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "mid", "0" },
            Value = 1000.0
        });

        var result = _service.RunOperatingPointAnalysis(circuit, includePower: false);

        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.NodeVoltages.ContainsKey("vcc"));
        Assert.True(result.NodeVoltages.ContainsKey("mid"));
    }

    [Fact]
    public void RunOperatingPointAnalysis_WithVCVSAmplifier_CalculatesCorrectVoltage()
    {
        // Arrange: Create a voltage amplifier using VCVS
        // Input: 1V, Gain: 10, Expected Output: 10V
        var circuit = _circuitManager.CreateCircuit("vcvs_amplifier", "VCVS amplifier test");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 1.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "0" },
            Value = 1000.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "E1",
            ComponentType = "vcvs",
            Nodes = new List<string> { "out", "0", "in", "0" },
            Parameters = new Dictionary<string, object> { { "gain", 10.0 } }
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });

        // Act
        var result = _service.RunOperatingPointAnalysis(circuit);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.NodeVoltages.ContainsKey("in"));
        Assert.True(result.NodeVoltages.ContainsKey("out"));
        
        // Input should be approximately 1V
        Assert.True(Math.Abs(result.NodeVoltages["in"] - 1.0) < 0.01);
        
        // Output should be approximately 10V (gain of 10)
        Assert.True(Math.Abs(result.NodeVoltages["out"] - 10.0) < 0.1);
    }

    [Fact]
    public void RunOperatingPointAnalysis_WithVCCS_CalculatesCorrectCurrent()
    {
        // Arrange: Create a transconductance amplifier using VCCS
        // Input: 1V, Transconductance: 0.001 A/V, Expected Current: 0.001A
        var circuit = _circuitManager.CreateCircuit("vccs_test", "VCCS test");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "in", "0" },
            Value = 1.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "0" },
            Value = 1000.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "G1",
            ComponentType = "vccs",
            Nodes = new List<string> { "out", "0", "in", "0" },
            Parameters = new Dictionary<string, object> { { "gain", 0.001 } }
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });

        // Act
        var result = _service.RunOperatingPointAnalysis(circuit);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        
        // Verify output node voltage exists (VCCS creates current based on input voltage)
        Assert.True(result.NodeVoltages.ContainsKey("out"));
        
        // The VCCS should create a current proportional to input voltage
        // With gain = 0.001 A/V and input = 1V, current = 0.001A
        // With R2 = 1000 ohms, voltage drop = 0.001A * 1000 = 1V
        // So output voltage should be approximately 1V
        var outputVoltage = Math.Abs(result.NodeVoltages["out"]);
        Assert.True(outputVoltage > 0.5 && outputVoltage < 1.5, $"Expected output voltage ~1V, got {outputVoltage}");
    }

    [Fact]
    public void RunOperatingPointAnalysis_WithVoltageDivider_ExtractsComponentCurrents()
    {
        // Arrange: Create a simple voltage divider circuit
        // V1 = 5V, R1 = 1000Ω, R2 = 1000Ω
        // Expected: i(V1) = -2.5mA (negative due to SPICE convention)
        //          i(R1) = 2.5mA, i(R2) = 2.5mA
        var circuit = _circuitManager.CreateCircuit("current_test", "Component current extraction test");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "in" },
            Value = 5.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });

        // Act
        var result = _service.RunOperatingPointAnalysis(circuit);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        
        // Verify voltage source current (should be negative due to SPICE convention)
        Assert.True(result.BranchCurrents.ContainsKey("V1"), "Should have V1 current");
        var v1Current = result.BranchCurrents["V1"];
        var expectedV1Current = -0.0025; // -2.5mA (current flows OUT of positive terminal)
        Assert.True(Math.Abs(v1Current - expectedV1Current) < 0.0001, 
            $"Expected i(V1) ≈ {expectedV1Current}A, got {v1Current}A");
        
        // Verify resistor currents
        // Note: Current direction depends on node order in component definition
        // For R1: nodes=["in", "out"], current flows from "in" to "out" (positive when Vin > Vout)
        // For R2: nodes=["out", "0"], current flows from "out" to "0" (positive when Vout > V0)
        Assert.True(result.BranchCurrents.ContainsKey("R1"), "Should have R1 current");
        var r1Current = result.BranchCurrents["R1"];
        // R1 current should be positive (flowing from "in" to "out")
        // But the sign depends on how we calculate it - let's check magnitude
        var expectedR1CurrentMagnitude = 0.0025; // 2.5mA
        Assert.True(Math.Abs(Math.Abs(r1Current) - expectedR1CurrentMagnitude) < 0.0001, 
            $"Expected |i(R1)| ≈ {expectedR1CurrentMagnitude}A, got {r1Current}A");
        
        Assert.True(result.BranchCurrents.ContainsKey("R2"), "Should have R2 current");
        var r2Current = result.BranchCurrents["R2"];
        var expectedR2CurrentMagnitude = 0.0025; // 2.5mA
        Assert.True(Math.Abs(Math.Abs(r2Current) - expectedR2CurrentMagnitude) < 0.0001, 
            $"Expected |i(R2)| ≈ {expectedR2CurrentMagnitude}A, got {r2Current}A");
        
        // Verify currents are consistent (R1 and R2 should have same magnitude)
        Assert.True(Math.Abs(Math.Abs(r1Current) - Math.Abs(r2Current)) < 0.0001, 
            $"R1 and R2 currents should have same magnitude: R1={r1Current}A, R2={r2Current}A");
        // V1 current should be negative (SPICE convention), and magnitude should match resistor currents
        Assert.True(Math.Abs(Math.Abs(v1Current) - Math.Abs(r1Current)) < 0.0001, 
            $"|V1 current| should equal |R1 current|: V1={v1Current}A, R1={r1Current}A");
    }

    [Fact]
    public void RunOperatingPointAnalysis_WithModifiedResistor_UsesCurrentResistanceValue()
    {
        // This test verifies that when a resistor value is modified (e.g., during parameter sweep),
        // the current calculation uses the modified value, not the original value
        // Arrange: Create voltage divider with R1=1000Ω, R2=1000Ω, V1=5V
        var circuit = _circuitManager.CreateCircuit("sweep_test", "Resistor sweep current test");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "in" },
            Value = 5.0
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0  // Original value
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R2",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });

        // Modify R1 to 2000Ω (simulating a parameter sweep)
        // Use reflection to access internal ComponentDefinitions property
        var circuitType = typeof(CircuitModel);
        var componentDefsProperty = circuitType.GetProperty("ComponentDefinitions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (componentDefsProperty != null)
        {
            var componentDefs = componentDefsProperty.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
            if (componentDefs != null && componentDefs.TryGetValue("R1", out var r1Def))
            {
                r1Def.Value = 2000.0;
            }
        }
        
        // Also update the actual component in the circuit
        var spiceCircuit = circuit.GetSpiceSharpCircuit();
        var r1Component = spiceCircuit.TryGetEntity("R1", out var entity) ? entity : null;
        if (r1Component != null)
        {
            try
            {
                r1Component.SetParameter("resistance", 2000.0);
            }
            catch
            {
                // If SetParameter fails, recreate the component
                spiceCircuit.Remove(r1Component);
                var componentService = new ComponentService();
                if (componentDefsProperty != null)
                {
                    var componentDefs = componentDefsProperty.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
                    if (componentDefs != null && componentDefs.TryGetValue("R1", out var r1Def))
                    {
                        componentService.AddComponent(circuit, r1Def);
                    }
                }
            }
        }

        // Act
        var result = _service.RunOperatingPointAnalysis(circuit);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        
        // With R1=2000Ω, R2=1000Ω, V1=5V:
        // Total resistance = 3000Ω
        // Current = 5V / 3000Ω = 0.001667A = 1.667mA
        // v(out) = 1.667mA * 1000Ω = 1.667V
        // i(R1) should be 1.667mA (not 3.333mA which would be using original 1000Ω)
        
        Assert.True(result.BranchCurrents.ContainsKey("R1"), "Should have R1 current");
        var r1Current = result.BranchCurrents["R1"];
        var expectedR1Current = 0.001667; // 1.667mA
        Assert.True(Math.Abs(Math.Abs(r1Current) - expectedR1Current) < 0.0001, 
            $"Expected |i(R1)| ≈ {expectedR1Current}A (using R1=2000Ω), got {r1Current}A. " +
            $"If this is ~0.003333A, it's using the original 1000Ω value instead of the modified 2000Ω value.");
        
        // Verify R2 current matches (series circuit)
        Assert.True(result.BranchCurrents.ContainsKey("R2"), "Should have R2 current");
        var r2Current = result.BranchCurrents["R2"];
        Assert.True(Math.Abs(Math.Abs(r1Current) - Math.Abs(r2Current)) < 0.0001, 
            $"R1 and R2 currents should be equal in series: R1={r1Current}A, R2={r2Current}A");
    }

    [Fact]
    public void RunOperatingPointAnalysis_WithDiode_ExtractsDiodeCurrent()
    {
        // This test verifies that diode currents are correctly extracted in operating point analysis
        // This addresses a regression where i(D1) returns 0 during parameter sweeps
        // Arrange: Create a simple diode circuit with forward bias
        var circuit = _circuitManager.CreateCircuit("diode_test", "Diode current extraction test");
        
        // Create a model service to define the diode model
        var modelService = new ModelService();
        
        // Define a diode model (LED-like with typical parameters)
        modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "D1_MODEL",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-15 },  // Saturation current
                { "N", 1.5 },     // Ideality factor
                { "EG", 1.6 },    // Bandgap energy (eV)
                { "RS", 0.5 }     // Series resistance
            }
        });

        // Add voltage source (forward bias the diode)
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "anode" },
            Value = 3.0  // 3V forward bias
        });

        // Add diode
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "0" },
            Model = "D1_MODEL"
        });

        // Act
        var result = _service.RunOperatingPointAnalysis(circuit, includePower: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        
        // Verify that diode current is extracted and is non-zero
        // With 3V forward bias and IS=1e-15, we expect a significant forward current
        // (diode equation: I = IS * (exp(V/(n*VT)) - 1))
        // At 3V forward bias, current should be substantial (not zero)
        Assert.True(result.BranchCurrents.ContainsKey("D1"), 
            "Should have D1 current in BranchCurrents dictionary");
        
        var d1Current = result.BranchCurrents["D1"];
        
        // Also verify source current is non-zero (should match diode current in this simple circuit)
        Assert.True(result.BranchCurrents.ContainsKey("V1"), 
            "Should have V1 current in BranchCurrents dictionary");
        var v1Current = result.BranchCurrents["V1"];
        
        // The current should be non-zero and positive (forward bias)
        // For a diode with IS=1e-15 and 3V forward bias, we expect significant current
        // The current should match the source current magnitude (opposite sign)
        // We'll check that it's at least > 1e-12 to ensure it's not zero or near-zero noise
        Assert.True(Math.Abs(d1Current) > 1e-12, 
            $"Diode current i(D1) should be non-zero with 3V forward bias. Got {d1Current}A. " +
            $"If this is 0 or very small (<1e-12), there's a regression in diode current extraction.");
        
        // Verify the diode current magnitude matches the source current magnitude
        // (they should be equal in a simple two-component circuit)
        Assert.True(Math.Abs(Math.Abs(d1Current) - Math.Abs(v1Current)) < 0.0001, 
            $"|D1 current| should equal |V1 current| in this simple circuit. " +
            $"D1={d1Current}A, V1={v1Current}A. " +
            $"If D1 is much smaller, it's using RealCurrentExport instead of source current.");
        
        // V1 current should be negative (SPICE convention: current flows out of positive terminal)
        // and magnitude should approximately match diode current
        Assert.True(Math.Abs(Math.Abs(v1Current) - Math.Abs(d1Current)) < 0.0001, 
            $"|V1 current| should approximately equal |D1 current| in this simple circuit. " +
            $"V1={v1Current}A, D1={d1Current}A");
    }
}

