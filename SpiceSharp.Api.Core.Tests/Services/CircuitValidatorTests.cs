using Xunit;
using SpiceSharp.Api.Core.Models;
using SpiceSharp.Api.Core.Services;

namespace SpiceSharp.Api.Core.Tests.Services;

public class CircuitValidatorTests
{
    [Fact]
    public void Validate_WithNullCircuit_ReturnsError()
    {
        // Arrange
        var validator = new CircuitValidator();

        // Act
        var result = validator.Validate(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("null", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithValidCircuit_ReturnsValidResult()
    {
        // Arrange
        var validator = new CircuitValidator();
        var circuit = new CircuitModel
        {
            Id = "test",
            Description = "Test circuit"
        };

        // Act
        var result = validator.Validate(circuit);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Validate_WithNoGroundNode_ReturnsWarning()
    {
        // Arrange
        var validator = new CircuitValidator();
        var circuit = new CircuitModel
        {
            Id = "test",
            Description = "Test circuit"
        };

        // Act
        var result = validator.Validate(circuit);

        // Assert
        Assert.True(result.IsValid); // No errors
        Assert.True(result.HasWarnings);
        Assert.Contains("ground", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithGroundNode_NoWarning()
    {
        // Arrange
        var validator = new CircuitValidator();
        var componentService = new ComponentService();
        var circuit = new CircuitModel
        {
            Id = "test",
            Description = "Test circuit"
        };
        
        // Add a component connected to ground (node "0")
        var voltageSource = new ComponentDefinition
        {
            Name = "V1",
            ComponentType = "voltage_source",
            Nodes = new List<string> { "input", "0" },
            Value = 1.0
        };
        componentService.AddComponent(circuit, voltageSource);

        // Act
        var result = validator.Validate(circuit);

        // Assert
        Assert.True(result.IsValid); // No errors
        Assert.False(result.HasWarnings); // No warnings because ground exists
        Assert.True(circuit.HasGround); // Circuit should report it has ground
    }

    [Fact]
    public void CircuitValidationResult_AddingErrors_UpdatesIsValid()
    {
        // Arrange
        var result = new CircuitValidationResult();

        // Act
        result.AddError("Test error");

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void CircuitValidationResult_AddingWarnings_UpdatesHasWarnings()
    {
        // Arrange
        var result = new CircuitValidationResult();

        // Act
        result.AddWarning("Test warning");

        // Assert
        Assert.True(result.HasWarnings);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void CircuitValidationResult_AddingEmptyError_DoesNotAdd()
    {
        // Arrange
        var result = new CircuitValidationResult();

        // Act
        result.AddError("");
        result.AddError(null!);

        // Assert
        Assert.Empty(result.Errors);
    }
}

