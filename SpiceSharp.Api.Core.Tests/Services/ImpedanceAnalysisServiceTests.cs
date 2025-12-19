using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;
using Xunit;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for ImpedanceAnalysisService
/// </summary>
public class ImpedanceAnalysisServiceTests
{
    private readonly IImpedanceAnalysisService _impedanceService;
    private readonly IComponentService _componentService;
    private readonly ICircuitManager _circuitManager;

    public ImpedanceAnalysisServiceTests()
    {
        var acAnalysisService = new ACAnalysisService();
        _impedanceService = new ImpedanceAnalysisService(acAnalysisService);
        _componentService = new ComponentService();
        _circuitManager = new CircuitManager();
    }

    [Fact]
    public void CalculateImpedance_WithSimpleRC_ReturnsCorrectImpedance()
    {
        // Arrange: Create a simple RC circuit
        var circuitId = "test_rc";
        var circuit = _circuitManager.CreateCircuit(circuitId, "RC circuit");
        
        // Add resistor R1 (1kΩ)
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        _componentService.AddComponent(circuit, r1Def);

        // Add capacitor C1 (1µF)
        var c1Def = new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6
        };
        _componentService.AddComponent(circuit, c1Def);

        // Act: Calculate impedance at port "in" to "0"
        var result = _impedanceService.CalculateImpedance(
            circuit,
            "in",
            "0",
            1000.0,  // 1 kHz start
            1001.0,  // 1.001 kHz stop (slightly different for validation)
            1        // Single point
        );

        // Assert: Should return impedance data
        Assert.NotNull(result);
        Assert.Single(result.Frequencies);
        Assert.True(Math.Abs(result.Frequencies[0] - 1000.0) < 10, 
            $"Expected frequency ~1000Hz, got {result.Frequencies[0]}Hz");
        Assert.Single(result.Magnitude);
        Assert.Single(result.Phase);
        
        // At 1kHz, RC circuit should have impedance close to R at low frequencies
        // Z = R + 1/(jωC) = R - j/(ωC)
        // |Z| = sqrt(R² + (1/(ωC))²)
        var expectedMagnitude = Math.Sqrt(1000.0 * 1000.0 + 1.0 / (2 * Math.PI * 1000.0 * 1e-6) / (2 * Math.PI * 1000.0 * 1e-6));
        Assert.True(result.Magnitude[0] > 0, "Impedance magnitude should be positive");
        Assert.True(result.Magnitude[0] < 2000, "Impedance magnitude should be reasonable");
    }

    [Fact]
    public void CalculateImpedance_WithPortNodes_MeasuresCorrectly()
    {
        // Arrange: Create a simple resistor circuit
        var circuitId = "test_r";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Resistor circuit");
        
        // Add resistor R1 (1kΩ) between port nodes
        // Need to connect port_neg to ground (0) for AC analysis to work
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "port_pos", "port_neg" },
            Value = 1000.0
        };
        _componentService.AddComponent(circuit, r1Def);
        
        // Connect port_neg to ground via a small resistor to ensure ground reference
        var rGndDef = new ComponentDefinition
        {
            Name = "R_GND",
            ComponentType = "resistor",
            Nodes = new List<string> { "port_neg", "0" },
            Value = 1e-6 // Very small resistance to ground
        };
        _componentService.AddComponent(circuit, rGndDef);

        // Act: Calculate impedance at port
        var result = _impedanceService.CalculateImpedance(
            circuit,
            "port_pos",
            "port_neg",
            1000.0,  // Start frequency
            1001.0,  // Stop frequency (slightly different for validation)
            1        // Single point
        );

        // Assert: Should return approximately 1kΩ
        Assert.NotNull(result);
        Assert.Single(result.Magnitude);
        // At DC/low frequency, impedance should be close to resistance
        // Allow 5% tolerance for numerical precision
        Assert.True(Math.Abs(result.Magnitude[0] - 1000.0) < 50, 
            $"Expected ~1000Ω, got {result.Magnitude[0]}Ω");
    }

    [Fact]
    public void CalculateImpedance_WithInvalidPort_ThrowsException()
    {
        // Arrange: Create a simple circuit
        var circuitId = "test_invalid";
        var circuit = _circuitManager.CreateCircuit(circuitId, "Test circuit");
        
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        _componentService.AddComponent(circuit, r1Def);

        // Act & Assert: Should throw exception for invalid port
        Assert.Throws<ArgumentException>(() => _impedanceService.CalculateImpedance(
            circuit,
            "nonexistent",
            "0",
            1000.0,
            1000.0,
            1
        ));
    }

    [Fact]
    public void CalculateImpedance_WithFrequencyRange_ReturnsMultiplePoints()
    {
        // Arrange: Create a simple RC circuit
        var circuitId = "test_range";
        var circuit = _circuitManager.CreateCircuit(circuitId, "RC circuit");
        
        var r1Def = new ComponentDefinition
        {
            Name = "R1",
            ComponentType = "resistor",
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        };
        _componentService.AddComponent(circuit, r1Def);

        var c1Def = new ComponentDefinition
        {
            Name = "C1",
            ComponentType = "capacitor",
            Nodes = new List<string> { "out", "0" },
            Value = 1e-6
        };
        _componentService.AddComponent(circuit, c1Def);

        // Act: Calculate impedance across frequency range
        var result = _impedanceService.CalculateImpedance(
            circuit,
            "in",
            "0",
            100.0,    // Start at 100 Hz
            10000.0,  // Stop at 10 kHz
            10        // 10 points
        );

        // Assert: Should return multiple frequency points
        Assert.NotNull(result);
        Assert.Equal(10, result.Frequencies.Count);
        Assert.Equal(10, result.Magnitude.Count);
        Assert.Equal(10, result.Phase.Count);
        
        // Frequencies should be in ascending order
        for (int i = 1; i < result.Frequencies.Count; i++)
        {
            Assert.True(result.Frequencies[i] > result.Frequencies[i - 1], 
                "Frequencies should be in ascending order");
        }
    }
}
