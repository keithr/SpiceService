using SpiceSharp.Api.Core.Models;
using SpiceSharp;
using SpiceSharp.Entities;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for managing components within a circuit.
/// </summary>
public interface IComponentService
{
    /// <summary>
    /// Adds a component to the circuit based on the provided definition.
    /// </summary>
    /// <param name="circuit">The circuit to add the component to</param>
    /// <param name="definition">The component definition</param>
    /// <returns>The created SpiceSharp entity</returns>
    /// <exception cref="ArgumentException">Thrown if component name already exists or validation fails</exception>
    IEntity AddComponent(CircuitModel circuit, ComponentDefinition definition);

    /// <summary>
    /// Gets a component by name from the circuit.
    /// </summary>
    /// <param name="circuit">The circuit</param>
    /// <param name="name">The component name</param>
    /// <returns>The entity if found, otherwise null</returns>
    IEntity? GetComponent(CircuitModel circuit, string name);

    /// <summary>
    /// Lists all components in the circuit.
    /// </summary>
    /// <param name="circuit">The circuit</param>
    /// <returns>Collection of all entities in the circuit</returns>
    IEnumerable<IEntity> ListComponents(CircuitModel circuit);

    /// <summary>
    /// Removes a component from the circuit by name.
    /// </summary>
    /// <param name="circuit">The circuit</param>
    /// <param name="name">The component name to remove</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveComponent(CircuitModel circuit, string name);

    /// <summary>
    /// Modifies a component's parameters in the circuit.
    /// </summary>
    /// <param name="circuit">The circuit</param>
    /// <param name="componentName">The component name</param>
    /// <param name="parameters">Dictionary of parameter names and values to update</param>
    /// <exception cref="ArgumentException">Thrown if component not found or parameter is invalid</exception>
    void ModifyComponent(CircuitModel circuit, string componentName, Dictionary<string, object> parameters);
}

