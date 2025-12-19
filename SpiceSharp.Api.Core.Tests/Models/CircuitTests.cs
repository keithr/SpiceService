using Xunit;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Tests.Models;

public class CircuitTests
{
    [Fact]
    public void Circuit_Creation_ShouldSetDefaultProperties()
    {
        // Arrange & Act
        var circuit = new CircuitModel
        {
            Id = "test_circuit",
            Description = "Test description"
        };

        // Assert
        Assert.Equal("test_circuit", circuit.Id);
        Assert.Equal("Test description", circuit.Description);
        Assert.False(circuit.IsActive);
        Assert.Equal(0, circuit.ComponentCount);
        Assert.False(circuit.HasGround);
    }

    [Fact]
    public void Circuit_Timestamps_ShouldBeSet()
    {
        // Arrange & Act
        var circuit = new CircuitModel
        {
            Id = "test_circuit"
        };

        // Assert
        Assert.NotEqual(default(DateTime), circuit.CreatedAt);
        Assert.NotEqual(default(DateTime), circuit.ModifiedAt);
    }

    [Fact]
    public void Circuit_Nodes_ShouldReturnEmptyInitially()
    {
        // Arrange
        var circuit = new CircuitModel { Id = "test_circuit" };

        // Act
        var nodes = circuit.Nodes;

        // Assert
        Assert.Empty(nodes);
    }
}

