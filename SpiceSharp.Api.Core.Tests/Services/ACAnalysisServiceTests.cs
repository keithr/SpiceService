using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

public class ACAnalysisServiceTests
{
    private readonly ACAnalysisService _service;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;

    public ACAnalysisServiceTests()
    {
        _service = new ACAnalysisService();
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
    }

    [Fact]
    public void RunACAnalysis_WithNullCircuit_ThrowsException()
    {
        var signals = new List<string> { "v(out)" };
        Assert.Throws<ArgumentNullException>(() => _service.RunACAnalysis(null!, 1, 1000, 10, signals));
    }

    [Fact]
    public void RunACAnalysis_WithNoSignals_ThrowsException()
    {
        var circuit = _circuitManager.CreateCircuit("test", "Test");
        Assert.Throws<ArgumentException>(() => _service.RunACAnalysis(circuit, 1, 1000, 10, null));
    }

    [Fact]
    public void RunACAnalysis_WithInvalidFrequencyRange_ThrowsException()
    {
        var circuit = _circuitManager.CreateCircuit("test", "Test");
        var signals = new List<string> { "v(out)" };
        Assert.Throws<ArgumentException>(() => _service.RunACAnalysis(circuit, 1000, 100, 10, signals));
    }

    [Fact]
    public void RunACAnalysis_SimpleRC_HasFrequencyResponse()
    {
        // Arrange: Create a simple RC low-pass filter
        var circuit = _circuitManager.CreateCircuit("rc_filter", "RC filter AC analysis");

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "0", "in" },
            Value = 1.0  // 1V AC (set via AC parameter in real SPICE)
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0  // 1 kΩ
        });

        _componentService.AddComponent(circuit, new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6  // 1 uF
        });

        // Act: Run AC analysis from 1 Hz to 1 kHz
        var signals = new List<string> { "v(out)" };
        var result = _service.RunACAnalysis(circuit, 1, 1000, 20, signals);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Frequencies.Count > 0, "Should have frequency points");
        Assert.True(result.MagnitudeDb.ContainsKey("v(out)"), "Should have magnitude data");
        Assert.True(result.PhaseDegrees.ContainsKey("v(out)"), "Should have phase data");
        
        // At low frequencies, output should be close to input (high magnitude)
        // At high frequencies, output should be attenuated (lower magnitude)
        Assert.True(result.MagnitudeDb["v(out)"].Count > 0, "Should have magnitude points");
    }
}

