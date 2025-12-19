using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

public class CircuitManagerTests
{
    [Fact]
    public void CreateCircuit_WithValidId_ReturnsCircuit()
    {
        // Arrange
        var manager = new CircuitManager();

        // Act
        var circuit = manager.CreateCircuit("circuit1", "Test circuit");

        // Assert
        Assert.NotNull(circuit);
        Assert.Equal("circuit1", circuit.Id);
        Assert.Equal("Test circuit", circuit.Description);
        Assert.True(circuit.IsActive); // First circuit should be active
    }

    [Fact]
    public void CreateCircuit_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var manager = new CircuitManager();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => manager.CreateCircuit(string.Empty, "Test"));
    }

    [Fact]
    public void CreateCircuit_WithDuplicateId_ThrowsArgumentException()
    {
        // Arrange
        var manager = new CircuitManager();
        manager.CreateCircuit("circuit1", "Test circuit");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => manager.CreateCircuit("circuit1", "Another circuit"));
    }

    [Fact]
    public void GetCircuit_WithExistingId_ReturnsCircuit()
    {
        // Arrange
        var manager = new CircuitManager();
        var createdCircuit = manager.CreateCircuit("circuit1", "Test circuit");

        // Act
        var retrievedCircuit = manager.GetCircuit("circuit1");

        // Assert
        Assert.NotNull(retrievedCircuit);
        Assert.Equal(createdCircuit.Id, retrievedCircuit.Id);
    }

    [Fact]
    public void GetCircuit_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var manager = new CircuitManager();

        // Act
        var circuit = manager.GetCircuit("nonexistent");

        // Assert
        Assert.Null(circuit);
    }

    [Fact]
    public void GetActiveCircuit_WithActiveCircuit_ReturnsCircuit()
    {
        // Arrange
        var manager = new CircuitManager();
        var createdCircuit = manager.CreateCircuit("circuit1", "Test circuit");

        // Act
        var activeCircuit = manager.GetActiveCircuit();

        // Assert
        Assert.NotNull(activeCircuit);
        Assert.Equal("circuit1", activeCircuit.Id);
    }

    [Fact]
    public void GetActiveCircuit_WithNoCircuits_ReturnsNull()
    {
        // Arrange
        var manager = new CircuitManager();

        // Act
        var activeCircuit = manager.GetActiveCircuit();

        // Assert
        Assert.Null(activeCircuit);
    }

    [Fact]
    public void SetActiveCircuit_WithValidId_ChangesActiveCircuit()
    {
        // Arrange
        var manager = new CircuitManager();
        var circuit1 = manager.CreateCircuit("circuit1", "Circuit 1");
        var circuit2 = manager.CreateCircuit("circuit2", "Circuit 2");

        // Act
        manager.SetActiveCircuit("circuit2");

        // Assert
        Assert.False(circuit1.IsActive);
        Assert.True(circuit2.IsActive);
        Assert.Equal("circuit2", manager.GetActiveCircuit()?.Id);
    }

    [Fact]
    public void SetActiveCircuit_WithNonExistentId_ThrowsArgumentException()
    {
        // Arrange
        var manager = new CircuitManager();
        manager.CreateCircuit("circuit1", "Test circuit");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => manager.SetActiveCircuit("nonexistent"));
    }

    [Fact]
    public void ListCircuits_ReturnsAllCircuits()
    {
        // Arrange
        var manager = new CircuitManager();
        manager.CreateCircuit("circuit1", "Circuit 1");
        manager.CreateCircuit("circuit2", "Circuit 2");
        manager.CreateCircuit("circuit3", "Circuit 3");

        // Act
        var circuits = manager.ListCircuits().ToList();

        // Assert
        Assert.Equal(3, circuits.Count);
        Assert.Contains(circuits, c => c.Id == "circuit1");
        Assert.Contains(circuits, c => c.Id == "circuit2");
        Assert.Contains(circuits, c => c.Id == "circuit3");
    }

    [Fact]
    public void ClearCircuit_WithExistingId_RemovesCircuit()
    {
        // Arrange
        var manager = new CircuitManager();
        manager.CreateCircuit("circuit1", "Circuit 1");
        manager.CreateCircuit("circuit2", "Circuit 2");

        // Act
        var result = manager.ClearCircuit("circuit1");

        // Assert
        Assert.True(result);
        Assert.Null(manager.GetCircuit("circuit1"));
        Assert.NotNull(manager.GetCircuit("circuit2"));
    }

    [Fact]
    public void ClearCircuit_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        var manager = new CircuitManager();

        // Act
        var result = manager.ClearCircuit("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ClearCircuit_WithActiveCircuit_ActivatesAnotherCircuit()
    {
        // Arrange最多
        var manager = new CircuitManager();
        var circuit1 = manager.CreateCircuit("circuit1", "Circuit 1");
        var circuit2 = manager.CreateCircuit("circuit2", "Circuit 2");

        // Act
        manager.ClearCircuit("circuit1");

        // Assert
        Assert.True(circuit2.IsActive);
        Assert.Equal("circuit2", manager.GetActiveCircuit()?.Id);
    }

    [Fact]
    public void ClearAllCircuits_RemovesAllCircuits()
    {
        // Arrange
        var manager = new CircuitManager();
        manager.CreateCircuit("circuit1", "Circuit 1");
        manager.CreateCircuit("circuit2", "Circuit 2");

        // Act
        manager.ClearAllCircuits();

        // Assert
        Assert.Empty(manager.ListCircuits());
        Assert.Null(manager.GetActiveCircuit());
    }

    [Fact]
    public void CreateCircuit_SecondCircuit_IsNotActive()
    {
        // Arrange
        var manager = new CircuitManager();
        var circuit1 = manager.CreateCircuit("circuit1", "Circuit 1");

        // Act
        var circuit2 = manager.CreateCircuit("circuit2", "Circuit 2");

        // Assert
        Assert.True(circuit1.IsActive);
        Assert.False(circuit2.IsActive);
    }
}

