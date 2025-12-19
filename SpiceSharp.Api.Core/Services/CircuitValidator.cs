using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Validates circuit topology and configuration for common issues.
/// </summary>
public class CircuitValidator
{
    /// <summary>
    /// Validates a circuit for common issues.
    /// </summary>
    /// <param name="circuit">The circuit to validate</param>
    /// <returns>Validation result with any issues found</returns>
    public CircuitValidationResult Validate(CircuitModel circuit)
    {
        var result = new CircuitValidationResult();

        if (circuit == null)
        {
            result.AddError("Circuit is null.");
            return result;
        }

        // Check for ground node
        if (!circuit.HasGround)
        {
            result.AddWarning("Circuit does not have a ground node (node '0'). This may cause simulation issues.");
        }

        // Check for floating nodes
        ValidateFloatingNodes(circuit, result);

        // Check for shorts (will be implemented when we have better access to circuit topology)
        // Check for DC paths (will be implemented when we have better access to circuit topology)

        return result;
    }

    /// <summary>
    /// Validates that the circuit doesn't have floating nodes.
    /// </summary>
    private void ValidateFloatingNodes(CircuitModel circuit, CircuitValidationResult result)
    {
        // TODO: Implement floating node detection once we have access to node information
        // For now, this is a placeholder
    }
}

/// <summary>
/// Result of circuit validation.
/// </summary>
public class CircuitValidationResult
{
    /// <summary>
    /// List of errors found during validation.
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// List of warnings found during validation.
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Whether the circuit is valid (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Whether the circuit has any warnings.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    public void AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            Errors.Add(error);
    }

    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    public void AddWarning(string warning)
    {
        if (!string.IsNullOrWhiteSpace(warning))
            Warnings.Add(warning);
    }
}

