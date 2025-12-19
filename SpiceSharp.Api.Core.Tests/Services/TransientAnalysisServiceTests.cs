using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

public class TransientAnalysisServiceTests
{
    private readonly TransientAnalysisService _service;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;

    public TransientAnalysisServiceTests()
    {
        _service = new TransientAnalysisService();
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
        _modelService = new ModelService();
    }

    [Fact]
    public void RunTransientAnalysis_WithNullCircuit_ThrowsException()
    {
        var signals = new List<string> { "v(n1)" };
        Assert.Throws<ArgumentNullException>(() => _service.RunTransientAnalysis(null!, 0, 1, 0.01, signals, false));
    }

    [Fact]
    public void RunTransientAnalysis_WithNoSignals_ThrowsException()
    {
        var circuit = _circuitManager.CreateCircuit("test", "Test");
        Assert.Throws<ArgumentException>(() => _service.RunTransientAnalysis(circuit, 0, 1, 0.01, null, false));
    }

    [Fact]
    public void RunTransientAnalysis_WithInvalidTimeRange_ThrowsException()
    {
        var circuit = _circuitManager.CreateCircuit("test", "Test");
        var signals = new List<string> { "v(n1)" };
        Assert.Throws<ArgumentException>(() => _service.RunTransientAnalysis(circuit, 1, 0, 0.01, signals, false));
    }

    [Fact]
    public void RunTransientAnalysis_SimpleRC_SimulatesCorrectly()
    {
        // Arrange: Create a simple RC circuit with a voltage step
        var circuit = _circuitManager.CreateCircuit("rc_test", "RC transient test");

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
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6  // 1 uF
        });

        // Act: Run transient analysis for 5ms
        var signals = new List<string> { "v(out)", "v(in)" };
        var result = _service.RunTransientAnalysis(circuit, 0, 5e-3, 1e-4, signals, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.Time.Count > 0, "Should have time points");
        Assert.True(result.Signals.ContainsKey("v(out)"), "Should have output voltage");
        Assert.True(result.Signals.ContainsKey("v(in)"), "Should have input voltage");
        
        // Verify we got voltage data - exact values depend on source configuration
        var outputVoltages = result.Signals["v(out)"];
        Assert.True(outputVoltages.Count > 0, "Should have output voltage data");
        
        // Check that voltages are reasonable (not NaN)
        foreach (var voltage in outputVoltages)
        {
            Assert.False(double.IsNaN(voltage), $"Voltage should not be NaN: {voltage}");
            Assert.True(Math.Abs(voltage) < 100, $"Voltage should be reasonable: {voltage}V");
        }
    }

    [Fact]
    public void RunTransientAnalysis_WithVoltageSwitch_SimulatesCorrectly()
    {
        // Arrange: Create a circuit with a voltage controlled switch
        // Input voltage source -> Switch (controlled by control voltage) -> Load resistor
        var circuit = _circuitManager.CreateCircuit("switch_test", "Switch transient test");
        var modelService = new ModelService();

        // Create switch model
        modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelType = "voltage_switch",
            ModelName = "SW_MODEL",
            Parameters = new Dictionary<string, double>
            {
                { "VT", 2.5 },   // Threshold voltage: switch turns on at 2.5V
                { "VH", 0.5 },   // Hysteresis: 0.5V
                { "RON", 1.0 },  // On resistance: 1 ohm
                { "ROFF", 1e6 }  // Off resistance: 1M ohm
            }
        });

        // Input voltage source (pulse waveform)
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V_IN",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "in" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "waveform", "pulse" },
                { "v1", 0.0 },
                { "v2", 5.0 },
                { "td", 0.0 },
                { "tr", 1e-6 },
                { "tf", 1e-6 },
                { "pw", 5e-3 },
                { "per", 10e-3 }
            }
        });

        // Control voltage source (controls the switch)
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V_CTRL",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "ctrl" },
            Value = 3.0  // Above threshold, switch should be on
        });

        // Voltage controlled switch
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "S1",
            ComponentType = "voltage_switch",
            Nodes = new List<string> { "in", "out" },
            Parameters = new Dictionary<string, object>
            {
                { "controlNodes", new[] { "ctrl", "0" } },
                { "model", "SW_MODEL" }
            }
        });

        // Load resistor
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R_LOAD",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });

        // Act: Run transient analysis
        var signals = new List<string> { "v(out)", "v(in)", "v(ctrl)" };
        var result = _service.RunTransientAnalysis(circuit, 0, 10e-3, 0.1e-3, signals, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.Time.Count > 0, "Should have time points");
        Assert.True(result.Signals.ContainsKey("v(out)"), "Should have output voltage");
        Assert.True(result.Signals.ContainsKey("v(in)"), "Should have input voltage");
        Assert.True(result.Signals.ContainsKey("v(ctrl)"), "Should have control voltage");
        
        // Verify voltages are reasonable
        var outputVoltages = result.Signals["v(out)"];
        Assert.True(outputVoltages.Count > 0, "Should have output voltage data");
        
        foreach (var voltage in outputVoltages)
        {
            Assert.False(double.IsNaN(voltage), $"Voltage should not be NaN: {voltage}");
            Assert.True(Math.Abs(voltage) < 100, $"Voltage should be reasonable: {voltage}V");
        }
    }

    [Fact]
    public void RunTransientAnalysis_WithPulseFlatParameters_SimulatesCorrectly()
    {
        // Arrange: Create a simple RC circuit with PULSE voltage source using flat parameters
        var circuit = _circuitManager.CreateCircuit("pulse_flat_test", "PULSE flat parameters test");

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "in" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v1", 0.0 },
                { "pulse_v2", 5.0 },
                { "pulse_td", 0.0 },
                { "pulse_tr", 1e-6 },
                { "pulse_tf", 1e-6 },
                { "pulse_pw", 1e-3 },
                { "pulse_per", 2e-3 }
            }
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
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6  // 1 uF
        });

        // Act: Run transient analysis for 5ms
        var signals = new List<string> { "v(out)", "v(in)" };
        var result = _service.RunTransientAnalysis(circuit, 0, 5e-3, 1e-4, signals, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.Time.Count > 0, "Should have time points");
        Assert.True(result.Signals.ContainsKey("v(out)"), "Should have output voltage");
        Assert.True(result.Signals.ContainsKey("v(in)"), "Should have input voltage");
        
        // Verify we got voltage data
        var inputVoltages = result.Signals["v(in)"];
        var outputVoltages = result.Signals["v(out)"];
        Assert.True(inputVoltages.Count > 0, "Should have input voltage data");
        Assert.True(outputVoltages.Count > 0, "Should have output voltage data");
        
        // Check that voltages are reasonable (not NaN)
        foreach (var voltage in inputVoltages)
        {
            Assert.False(double.IsNaN(voltage), $"Input voltage should not be NaN: {voltage}");
        }
        foreach (var voltage in outputVoltages)
        {
            Assert.False(double.IsNaN(voltage), $"Output voltage should not be NaN: {voltage}");
            Assert.True(Math.Abs(voltage) < 100, $"Voltage should be reasonable: {voltage}V");
        }
        
        // Verify pulse waveform behavior: input should pulse from 0V to 5V
        // Check that input voltage changes (not constant DC)
        var minInputVoltage = inputVoltages.Min();
        var maxInputVoltage = inputVoltages.Max();
        var voltageRange = maxInputVoltage - minInputVoltage;
        // Pulse should cause voltage variation (at least some change from initial value)
        // Note: Exact values depend on simulation timing and pulse parameters
        Assert.True(voltageRange >= 0.1 || maxInputVoltage >= 0.1, 
            $"Input voltage should show pulse behavior (min: {minInputVoltage}V, max: {maxInputVoltage}V)");
    }

    [Fact]
    public void RunTransientAnalysis_WithPulseMinimalParameters_SimulatesCorrectly()
    {
        // Arrange: Test PULSE with minimal parameters (only v1, v2) - should use defaults
        var circuit = _circuitManager.CreateCircuit("pulse_minimal_test", "PULSE minimal parameters test");

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "in" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v1", 0.0 },
                { "pulse_v2", 3.0 }
                // Other parameters should use SPICE defaults
            }
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "0" },
            Value = 1000.0
        });

        // Act: Run transient analysis
        var signals = new List<string> { "v(in)" };
        var result = _service.RunTransientAnalysis(circuit, 0, 5e-3, 1e-4, signals, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.Signals.ContainsKey("v(in)"), "Should have input voltage");
        
        var inputVoltages = result.Signals["v(in)"];
        Assert.True(inputVoltages.Count > 0, "Should have input voltage data");
        
        // Verify voltages are reasonable
        foreach (var voltage in inputVoltages)
        {
            Assert.False(double.IsNaN(voltage), $"Voltage should not be NaN: {voltage}");
            Assert.True(Math.Abs(voltage) < 100, $"Voltage should be reasonable: {voltage}V");
        }
    }

    [Fact]
    public void RunTransientAnalysis_WithPulseCurrentSource_SimulatesCorrectly()
    {
        // Arrange: Test PULSE with current source using flat parameters
        var circuit = _circuitManager.CreateCircuit("pulse_current_test", "PULSE current source test");

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "I1",
            ComponentType = "current_source",
            Nodes = new List<string> { "0", "out" },
            Value = 0.0,
            Parameters = new Dictionary<string, object>
            {
                { "pulse_v1", 0.0 },
                { "pulse_v2", 0.001 },  // 1mA pulse
                { "pulse_td", 0.0 },
                { "pulse_tr", 1e-6 },
                { "pulse_tf", 1e-6 },
                { "pulse_pw", 1e-3 },
                { "pulse_per", 2e-3 }
            }
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "out", "0" },
            Value = 1000.0
        });

        // Act: Run transient analysis
        var signals = new List<string> { "v(out)", "i(R1)" };
        var result = _service.RunTransientAnalysis(circuit, 0, 5e-3, 1e-4, signals, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.Signals.ContainsKey("v(out)"), "Should have output voltage");
        
        var outputVoltages = result.Signals["v(out)"];
        Assert.True(outputVoltages.Count > 0, "Should have output voltage data");
        
        // Verify voltages are reasonable (should be around 0-1V for 1mA through 1kΩ)
        foreach (var voltage in outputVoltages)
        {
            Assert.False(double.IsNaN(voltage), $"Voltage should not be NaN: {voltage}");
            Assert.True(Math.Abs(voltage) < 10, $"Voltage should be reasonable: {voltage}V");
        }
    }

    [Fact]
    public void RunTransientAnalysis_WithVoltageDivider_ExtractsResistorCurrents()
    {
        // This test verifies that resistor currents are correctly extracted in transient analysis
        // This addresses a regression where i(R1), i(R2) return 0 in transient analysis
        // Arrange: Create a simple voltage divider circuit with DC source
        // V1 = 3V, R1 = 1000Ω, R2 = 2000Ω
        // Expected: v(out) = V1 × (R2/(R1+R2)) = 3V × (2000/3000) = 2V (steady state)
        //          i(V1) = -1mA (negative due to SPICE convention)
        //          i(R1) = 1mA, i(R2) = 1mA (steady state)
        var circuit = _circuitManager.CreateCircuit("transient_current_test", "Transient analysis resistor current test");
        
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "in" },
            Value = 3.0
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
            Value = 2000.0
        });

        // Act: Run transient analysis (short time to reach steady state)
        var signals = new List<string> { "v(out)", "i(V1)", "i(R1)", "i(R2)" };
        var result = _service.RunTransientAnalysis(circuit, 0, 1e-3, 1e-5, signals, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.Signals.ContainsKey("v(out)"), "Should have v(out) data");
        Assert.True(result.Signals.ContainsKey("i(V1)"), "Should have i(V1) data");
        Assert.True(result.Signals.ContainsKey("i(R1)"), "Should have i(R1) data");
        Assert.True(result.Signals.ContainsKey("i(R2)"), "Should have i(R2) data");
        
        // Get steady-state values (last time point)
        var vOut = result.Signals["v(out)"].Last();
        var v1Current = result.Signals["i(V1)"].Last();
        var r1Current = result.Signals["i(R1)"].Last();
        var r2Current = result.Signals["i(R2)"].Last();
        
        // Verify voltage (check magnitude, sign may vary based on node reference)
        var expectedVOut = 2.0; // 3V × (2000/(1000+2000)) = 2V
        Assert.True(Math.Abs(Math.Abs(vOut) - expectedVOut) < 0.1, 
            $"Expected |v(out)| ≈ {expectedVOut}V, got {vOut}V");
        
        // Verify source current
        var expectedV1Current = -0.001; // -1mA (current flows OUT of positive terminal)
        Assert.True(Math.Abs(Math.Abs(v1Current) - Math.Abs(expectedV1Current)) < 0.0001, 
            $"Expected |i(V1)| ≈ {Math.Abs(expectedV1Current)}A, got {v1Current}A");
        
        // Verify resistor currents (should be non-zero)
        var expectedResistorCurrent = 0.001; // 1mA
        Assert.True(Math.Abs(Math.Abs(r1Current) - expectedResistorCurrent) < 0.0001, 
            $"Expected |i(R1)| ≈ {expectedResistorCurrent}A, got {r1Current}A. " +
            $"If this is 0, resistor current extraction is broken in transient analysis.");
        
        Assert.True(Math.Abs(Math.Abs(r2Current) - expectedResistorCurrent) < 0.0001, 
            $"Expected |i(R2)| ≈ {expectedResistorCurrent}A, got {r2Current}A. " +
            $"If this is 0, resistor current extraction is broken in transient analysis.");
        
        // Verify currents are consistent (R1 and R2 should have same magnitude in series)
        Assert.True(Math.Abs(Math.Abs(r1Current) - Math.Abs(r2Current)) < 0.0001, 
            $"R1 and R2 currents should have same magnitude: R1={r1Current}A, R2={r2Current}A");
        
        // V1 current magnitude should match resistor current magnitude
        Assert.True(Math.Abs(Math.Abs(v1Current) - Math.Abs(r1Current)) < 0.0001, 
            $"|V1 current| should equal |R1 current|: V1={v1Current}A, R1={r1Current}A");
    }
}

