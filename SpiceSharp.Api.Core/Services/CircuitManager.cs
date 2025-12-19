using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Default implementation of circuit management service.
/// Manages multiple circuit instances with activation support.
/// </summary>
public class CircuitManager : ICircuitManager
{
    private readonly Dictionary<string, CircuitModel> _circuits = new();
    private string? _activeCircuitId;

    /// <inheritdoc/>
    public CircuitModel CreateCircuit(string id, string description)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Circuit ID cannot be null or empty.", nameof(id));

        if (_circuits.ContainsKey(id))
            throw new ArgumentException($"A circuit with ID '{id}' already exists.", nameof(id));

        var circuit = new CircuitModel
        {
            Id = id,
            Description = description ?? string.Empty,
            IsActive = false
        };

        _circuits[id] = circuit;

        // If this is the first circuit, make it active
        if (_activeCircuitId == null)
        {
            _activeCircuitId = id;
            circuit.IsActive = true;
        }

        return circuit;
    }

    /// <inheritdoc/>
    public CircuitModel? GetCircuit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _circuits.TryGetValue(id, out var circuit) ? circuit : null;
    }

    /// <inheritdoc/>
    public CircuitModel? GetActiveCircuit()
    {
        if (_activeCircuitId == null)
            return null;

        return _circuits.TryGetValue(_activeCircuitId, out var circuit) ? circuit : null;
    }

    /// <inheritdoc/>
    public void SetActiveCircuit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Circuit ID cannot be null or empty.", nameof(id));

        if (!_circuits.ContainsKey(id))
            throw new ArgumentException($"Circuit with ID '{id}' does not exist.", nameof(id));

        // Deactivate current active circuit
        if (_activeCircuitId != null && _circuits.TryGetValue(_activeCircuitId, out var previousCircuit))
        {
            previousCircuit.IsActive = false;
        }

        // Activate new circuit
        _activeCircuitId = id;
        if (_circuits.TryGetValue(id, out var newCircuit))
        {
            newCircuit.IsActive = true;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<CircuitModel> ListCircuits()
    {
        return _circuits.Values.ToList();
    }

    /// <inheritdoc/>
    public bool ClearCircuit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!_circuits.TryGetValue(id, out var circuit))
            return false;

        // If clearing the active circuit, clear the active circuit ID
        if (_activeCircuitId == id)
        {
            _activeCircuitId = null;
        }

        circuit.IsActive = false;
        _circuits.Remove(id);

        // If there are remaining circuits and none is active, activate the first one
        if (_circuits.Count > 0 && _activeCircuitId == null)
        {
            var firstCircuit = _circuits.Values.First();
            _activeCircuitId = firstCircuit.Id;
            firstCircuit.IsActive = true;
        }

        return true;
    }

    /// <inheritdoc/>
    public void ClearAllCircuits()
    {
        // Deactivate all circuits
        foreach (var circuit in _circuits.Values)
        {
            circuit.IsActive = false;
        }

        _circuits.Clear();
        _activeCircuitId = null;
    }
}

