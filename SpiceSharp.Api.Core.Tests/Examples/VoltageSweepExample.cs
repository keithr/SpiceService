using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Examples;

public class VoltageSweepExample
{
    [Fact]
    public void VoltageSweep_DemonstrateResistorIV()
    {
        var circuitManager = new CircuitManager();
        var componentService = new ComponentService();
        var dcService = new DCAnalysisService();

        var circuit = circuitManager.CreateCircuit("sweep_demo", "Voltage sweep");

        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "node1" },
            Value = 5.0
        });

        componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "node1", "0" },
            Value = 1000.0
        });

        var exports = new List<string> { "i(R1)" };

        var result = dcService.RunDCAnalysis(
            circuit,
            sourceName: "V1",
            startValue: 0.0,
            stopValue: 5.0,
            stepValue: 0.5,
            exports: exports);

        System.Console.WriteLine("\n=== Voltage Sweep Results ===");
        System.Console.WriteLine("Voltage (V)\tCurrent (mA)");
        System.Console.WriteLine("----------------------------");

        for (int i = 0; i < result.SweepValues.Count && i < result.Results["i(R1)"].Count; i++)
        {
            var voltage = result.SweepValues[i];
            var currentMa = result.Results["i(R1)"][i] * 1000.0;
            System.Console.WriteLine($"{voltage:F1}\t\t{currentMa:F2}");
        }

        System.Console.WriteLine("\n✓ DC Sweep working!");
        
        Assert.Equal("Success", result.Status);
    }
}

