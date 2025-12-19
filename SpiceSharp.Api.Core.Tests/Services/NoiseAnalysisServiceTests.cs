using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

/// <summary>
/// Tests for NoiseAnalysisService
/// </summary>
public class NoiseAnalysisServiceTests
{
    private readonly NoiseAnalysisService _service;
    private readonly CircuitManager _circuitManager;
    private readonly ComponentService _componentService;

    public NoiseAnalysisServiceTests()
    {
        _service = new NoiseAnalysisService();
        _circuitManager = new CircuitManager();
        _componentService = new ComponentService();
    }

    [Fact]
    public void RunNoiseAnalysis_WithNullCircuit_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            _service.RunNoiseAnalysis(null!, 1.0, 1e6, 100, "out", "V1"));
    }

    [Fact]
    public void RunNoiseAnalysis_WithEmptyOutputNode_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test", "Test circuit");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _service.RunNoiseAnalysis(circuit, 1.0, 1e6, 100, "", "V1"));
    }

    [Fact]
    public void RunNoiseAnalysis_WithEmptyInputSource_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test", "Test circuit");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _service.RunNoiseAnalysis(circuit, 1.0, 1e6, 100, "out", ""));
    }

    [Fact]
    public void RunNoiseAnalysis_WithInvalidFrequencyRange_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test", "Test circuit");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _service.RunNoiseAnalysis(circuit, 1e6, 1.0, 100, "out", "V1"));
    }

    [Fact]
    public void RunNoiseAnalysis_WithZeroPoints_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test", "Test circuit");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _service.RunNoiseAnalysis(circuit, 1.0, 1e6, 0, "out", "V1"));
    }

    [Fact]
    public void RunNoiseAnalysis_WithNegativePoints_ThrowsArgumentException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test", "Test circuit");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _service.RunNoiseAnalysis(circuit, 1.0, 1e6, -1, "out", "V1"));
    }

    [Fact]
    public void RunNoiseAnalysis_WithValidParameters_ThrowsNotImplementedException()
    {
        // Arrange
        var circuit = _circuitManager.CreateCircuit("test", "Test circuit");
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
            Nodes = new List<string> { "in", "out" },
            Value = 1000.0
        });

        // Act & Assert
        var ex = Assert.Throws<NotImplementedException>(() => 
            _service.RunNoiseAnalysis(circuit, 1.0, 1e6, 100, "out", "V1"));
        
        Assert.Contains("not yet supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

