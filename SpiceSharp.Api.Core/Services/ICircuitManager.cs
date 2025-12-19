using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Manages multiple circuit instances, allowing creation, retrieval, activation, and deletion.
/// </summary>
public interface ICircuitManager
{
    /// <summary>
    /// Creates a new circuit with the specified ID and description.
    /// </summary>
    /// <param name="id">Unique identifier for the circuit</param>
    /// <param name="description">Human-readable description</param>
    /// <returns>The created circuit instance</returns>
    /// <exception cref="ArgumentException">Thrown if a circuit with the given ID already exists</exception>
    CircuitModel CreateCircuit(string id, string description);

    /// <summary>
    /// Retrieves a circuit by its ID.
    /// </summary>
    /// <param name="id">The circuit ID</param>
    /// <returns>The circuit instance, or null if not found</returns>
    CircuitModel? GetCircuit(string id);

    /// <summary>
    /// Gets the currently active circuit.
    /// </summary>
    /// <returns>The active circuit, or null if no circuit is active</returns>
    CircuitModel? GetActiveCircuit();

    /// <summary>
    /// Sets the active circuit by ID.
    /// </summary>
    /// <param name="id">The circuit ID to activate</param>
    /// <exception cref="ArgumentException">Thrown if the circuit with the given ID does not exist</exception>
    void SetActiveCircuit(string id);

    /// <summary>
    /// Lists all circuits managed by this manager.
    /// </summary>
    /// <returns>Collection of all circuits</returns>
    IEnumerable<CircuitModel> ListCircuits();

    /// <summary>
    /// Clears (removes) a specific circuit by ID.
    /// </summary>
    /// <param name="id">The circuit ID to remove</param>
    /// <returns>True if the circuit was removed, false if it didn't exist</returns>
    bool ClearCircuit(string id);

    /// <summary>
    /// Clears (removes) all circuits managed by this manager.
    /// </summary>
    void ClearAllCircuits();
}

