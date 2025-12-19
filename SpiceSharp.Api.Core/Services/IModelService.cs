using SpiceSharp.Api.Core.Models;
using SpiceSharp.Entities;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for managing models within a circuit.
/// </summary>
public interface IModelService
{
    /// <summary>
    /// Defines a model in the circuit.
    /// </summary>
    /// <param name="circuit">The circuit to add the model to</param>
    /// <param name="definition">The model definition</param>
    /// <returns>The created SpiceSharp model entity</returns>
    /// <exception cref="ArgumentException">Thrown if model name already exists or validation fails</exception>
    IEntity DefineModel(CircuitModel circuit, ModelDefinition definition);

    /// <summary>
    /// Gets a model by name from the circuit.
    /// </summary>
    /// <param name="circuit">The circuit</param>
    /// <param name="name">The model name</param>
    /// <returns>The model entity if found, otherwise null</returns>
    IEntity? GetModel(CircuitModel circuit, string name);

    /// <summary>
    /// Lists all models in the circuit.
    /// </summary>
    /// <param name="circuit">The circuit</param>
    /// <returns>Collection of all model entities in the circuit</returns>
    IEnumerable<IEntity> ListModels(CircuitModel circuit);
}

