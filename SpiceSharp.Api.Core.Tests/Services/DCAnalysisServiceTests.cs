using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using System.Linq;

namespace SpiceSharp.Api.Core.Tests.Services;

public class DCAnalysisServiceTests
{
    private readonly DCAnalysisService _service;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;
    private readonly ModelService _modelService;

    public DCAnalysisServiceTests()
    {
        _service = new DCAnalysisService();
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
        _modelService = new ModelService();
    }

    [Fact]
    public void RunDCAnalysis_DiodeIVCurve_MeasuresCurrentThroughDiode()
    {
        // Arrange: Create a circuit with a diode and voltage source
        var circuit = _circuitManager.CreateCircuit("diode_test", "Diode IV curve");
        
        // Define diode model
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "1N4148",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-12 },
                { "N", 1.5 }
            }
        });

        // Add voltage source
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "anode" },
            Value = 0.0
        });

        // Add diode
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "anode", "0" },
            Model = "1N4148"
        });

        // Act: Sweep voltage from 0 to 0.8V and measure current
        var exports = new List<string> { "i(D1)" };
        var result = _service.RunDCAnalysis(
            circuit, 
            sourceName: "V1",
            startValue: 0.0,
            stopValue: 0.8,
            stepValue: 0.1,
            exports: exports);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.SweepValues.Count > 0, "Should have sweep values");
        Assert.True(result.Results.ContainsKey("i(D1)"), "Should have diode current data");
        Assert.Equal("A", result.Units["i(D1)"]);

        // Display results
        System.Console.WriteLine("\n=== Diode IV Curve (1N4148) ===");
        System.Console.WriteLine("Voltage (V)\tCurrent (mA)");
        System.Console.WriteLine("----------------------------");
        
        var currents = result.Results["i(D1)"];
        for (int i = 0; i < result.SweepValues.Count && i < currents.Count; i++)
        {
            var voltage = result.SweepValues[i];
            var currentMa = currents[i] * 1000.0;
            System.Console.WriteLine($"{voltage:F1}\t\t{currentMa:E2}");
        }

        System.Console.WriteLine($"\nTest completed with {currents.Count} data points.");
    }

    [Fact]
    public void RunDCAnalysis_LEDIVCurve_SweepsFrom0To4V()
    {
        // Arrange: Create a circuit with a red LED and voltage source
        var circuit = _circuitManager.CreateCircuit("led_test", "LED IV curve");
        
        // Define red LED model with typical parameters (matching TestLED.cs)
        _modelService.DefineModel(circuit, new ModelDefinition
        {
            ModelName = "RED_LED",
            ModelType = "diode",
            Parameters = new Dictionary<string, double>
            {
                { "IS", 1e-3 },     // Saturation current (extremely high for LED)
                { "N", 2.0 },        // Emission coefficient
                { "EG", 1.6 }        // Bandgap energy in eV
                // Note: RS not set here, matching TestLED.cs
            }
        });

        // Add voltage source
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "anode" },
            Value = 0.0
        });

        // Add small series resistor (50 ohms) - matching TestLED.cs
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "anode", "led_anode" },
            Value = 50.0
        });

        // Add LED
        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "D1",
            ComponentType = "diode",
            Nodes = new List<string> { "led_anode", "0" },
            Model = "RED_LED"
        });

        // Act: Sweep voltage from 0 to 5V and measure current (matching TestLED.cs)
        var exports = new List<string> { "i(D1)" };
        var result = _service.RunDCAnalysis(
            circuit, 
            sourceName: "V1",
            startValue: 0.0,
            stopValue: 5.0,
            stepValue: 0.1,
            exports: exports);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.SweepValues.Count > 0, "Should have sweep values");
        Assert.True(result.Results.ContainsKey("i(D1)"), "Should have LED current data");
        Assert.Equal("A", result.Units["i(D1)"]);

        var currents = result.Results["i(D1)"];
        Assert.True(currents.Count > 0, "Should have current data points");
        
        // Verify that currents are actually measured (not all zeros)
        // At higher voltages, LED should conduct current
        var maxCurrent = currents.Max();
        Assert.True(maxCurrent > 0, $"LED should conduct current at higher voltages. Max current: {maxCurrent} A");
        
        // Verify current increases with voltage (typical LED behavior)
        // Check that current at higher voltage is greater than at lower voltage
        var lowVoltageIndex = Math.Min(10, currents.Count - 1); // Around 1V
        var highVoltageIndex = Math.Min(40, currents.Count - 1); // Around 4V
        if (highVoltageIndex > lowVoltageIndex)
        {
            var lowVoltageCurrent = currents[lowVoltageIndex];
            var highVoltageCurrent = currents[highVoltageIndex];
            Assert.True(highVoltageCurrent > lowVoltageCurrent, 
                $"Current should increase with voltage. At {result.SweepValues[lowVoltageIndex]:F1}V: {lowVoltageCurrent:E6}A, " +
                $"At {result.SweepValues[highVoltageIndex]:F1}V: {highVoltageCurrent:E6}A");
        }

        // Display results
        System.Console.WriteLine("\n=== LED IV Curve (Red LED) ===");
        System.Console.WriteLine("Voltage (V)\tCurrent (A)\tCurrent (mA)");
        System.Console.WriteLine("--------------------------------------------");
        
        for (int i = 0; i < result.SweepValues.Count && i < currents.Count; i++)
        {
            var voltage = result.SweepValues[i];
            var current = currents[i];
            var currentMa = current * 1000.0;
            System.Console.WriteLine($"{voltage:F1}\t\t{current:E6}\t{currentMa:F6}");
        }

        System.Console.WriteLine($"\nTest completed with {currents.Count} data points.");
        System.Console.WriteLine($"Max current: {maxCurrent * 1000:F6} mA");
    }

    [Fact]
    public void RunDCAnalysis_WithNullCircuit_ThrowsException()
    {
        var exports = new List<string> { "i(R1)" };
        Assert.Throws<ArgumentNullException>(() => 
            _service.RunDCAnalysis(null!, "V1", 0, 1, 0.1, exports));
    }

    [Fact]
    public void RunDCAnalysis_WithNoExports_ThrowsException()
    {
        var circuit = _circuitManager.CreateCircuit("test", "Test");
        
        Assert.Throws<ArgumentException>(() => 
            _service.RunDCAnalysis(circuit, "V1", 0, 1, 0.1, null));
    }

    [Fact]
    public void RunDCAnalysis_WithVoltageDivider_ExtractsResistorCurrents()
    {
        // This test verifies that resistor currents are correctly extracted in DC analysis
        // This addresses a regression where i(R1), i(R2) return 0 in DC sweeps
        // Arrange: Create a simple voltage divider circuit
        // V1 = 3V, R1 = 1000Ω, R2 = 2000Ω
        // Expected: v(out) = V1 × (R2/(R1+R2)) = 3V × (2000/3000) = 2V
        //          i(V1) = -1mA (negative due to SPICE convention)
        //          i(R1) = 1mA, i(R2) = 1mA
        var circuit = _circuitManager.CreateCircuit("dc_current_test", "DC analysis resistor current test");
        
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

        // Act: Run DC sweep from 3V to 3V (single point, but tests the infrastructure)
        var exports = new List<string> { "v(out)", "i(V1)", "i(R1)", "i(R2)" };
        var result = _service.RunDCAnalysis(
            circuit,
            sourceName: "V1",
            startValue: 3.0,
            stopValue: 3.0,
            stepValue: 0.1,
            exports: exports);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.True(result.Results.ContainsKey("v(out)"), "Should have v(out) data");
        Assert.True(result.Results.ContainsKey("i(V1)"), "Should have i(V1) data");
        Assert.True(result.Results.ContainsKey("i(R1)"), "Should have i(R1) data");
        Assert.True(result.Results.ContainsKey("i(R2)"), "Should have i(R2) data");
        
        // Verify voltage (check magnitude, sign may vary based on node reference)
        var vOut = result.Results["v(out)"].First();
        var expectedVOut = 2.0; // 3V × (2000/(1000+2000)) = 2V
        Assert.True(Math.Abs(Math.Abs(vOut) - expectedVOut) < 0.01, 
            $"Expected |v(out)| ≈ {expectedVOut}V, got {vOut}V");
        
        // Verify source current
        var v1Current = result.Results["i(V1)"].First();
        var expectedV1Current = -0.001; // -1mA (current flows OUT of positive terminal)
        Assert.True(Math.Abs(v1Current - expectedV1Current) < 0.0001, 
            $"Expected i(V1) ≈ {expectedV1Current}A, got {v1Current}A");
        
        // Verify resistor currents (should be non-zero)
        var r1Current = result.Results["i(R1)"].First();
        var r2Current = result.Results["i(R2)"].First();
        
        // Both resistors should have 1mA current (series circuit)
        var expectedResistorCurrent = 0.001; // 1mA
        Assert.True(Math.Abs(Math.Abs(r1Current) - expectedResistorCurrent) < 0.0001, 
            $"Expected |i(R1)| ≈ {expectedResistorCurrent}A, got {r1Current}A. " +
            $"If this is 0, resistor current extraction is broken in DC analysis.");
        
        Assert.True(Math.Abs(Math.Abs(r2Current) - expectedResistorCurrent) < 0.0001, 
            $"Expected |i(R2)| ≈ {expectedResistorCurrent}A, got {r2Current}A. " +
            $"If this is 0, resistor current extraction is broken in DC analysis.");
        
        // Verify currents are consistent (R1 and R2 should have same magnitude in series)
        Assert.True(Math.Abs(Math.Abs(r1Current) - Math.Abs(r2Current)) < 0.0001, 
            $"R1 and R2 currents should have same magnitude: R1={r1Current}A, R2={r2Current}A");
        
        // V1 current magnitude should match resistor current magnitude
        Assert.True(Math.Abs(Math.Abs(v1Current) - Math.Abs(r1Current)) < 0.0001, 
            $"|V1 current| should equal |R1 current|: V1={v1Current}A, R1={r1Current}A");
    }
}
