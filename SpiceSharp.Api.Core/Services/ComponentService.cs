using SpiceSharp;
using SpiceSharp.Entities;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for managing components within circuits.
/// </summary>
public class ComponentService : IComponentService
{
    private readonly ComponentFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentService"/> class.
    /// </summary>
    public ComponentService()
    {
        _factory = new ComponentFactory();
    }

    /// <inheritdoc/>
    public IEntity AddComponent(CircuitModel circuit, ComponentDefinition definition)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        // Check for duplicate component name
        if (GetComponent(circuit, definition.Name) != null)
            throw new ArgumentException($"Component with name '{definition.Name}' already exists in the circuit.");

        // Special validation for mutual inductance - both inductors must exist
        if (definition.ComponentType.Equals("mutual_inductance", StringComparison.OrdinalIgnoreCase))
        {
            ValidateMutualInductance(circuit, definition);
        }

        // Create the SpiceSharp entity using the factory
        var entity = _factory.CreateComponent(definition);

        // Store the definition for export and metadata tracking
        circuit.ComponentDefinitions[definition.Name] = definition;

        // Add to the circuit
        circuit.InternalCircuit.Add(entity);

        // Update modified timestamp
        circuit.ModifiedAt = DateTime.UtcNow;

        return entity;
    }

    /// <inheritdoc/>
    public IEntity? GetComponent(CircuitModel circuit, string name)
    {
        if (circuit == null)
            return null;

        if (string.IsNullOrWhiteSpace(name))
            return null;

        return circuit.InternalCircuit.TryGetEntity(name, out var entity) ? entity : null;
    }

    /// <inheritdoc/>
    public IEnumerable<IEntity> ListComponents(CircuitModel circuit)
    {
        if (circuit == null)
            return Enumerable.Empty<IEntity>();

        return circuit.InternalCircuit;
    }

    /// <inheritdoc/>
    public bool RemoveComponent(CircuitModel circuit, string name)
    {
        if (circuit == null)
            return false;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        var entity = GetComponent(circuit, name);
        if (entity == null)
            return false;

        // Remove from circuit
        circuit.InternalCircuit.Remove(entity);

        // Also remove the stored definition
        circuit.ComponentDefinitions.Remove(name);

        // Update modified timestamp
        circuit.ModifiedAt = DateTime.UtcNow;

        return true;
    }

    /// <inheritdoc/>
    public void ModifyComponent(CircuitModel circuit, string componentName, Dictionary<string, object> parameters)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (string.IsNullOrWhiteSpace(componentName))
            throw new ArgumentException("Component name cannot be null or empty.", nameof(componentName));

        if (parameters == null || parameters.Count == 0)
            throw new ArgumentException("At least one parameter must be specified.", nameof(parameters));

        // Get the component entity
        var component = GetComponent(circuit, componentName);
        if (component == null)
        {
            var availableComponents = circuit.ComponentDefinitions.Keys.ToList();
            var componentList = availableComponents.Count > 0 
                ? $" Available components: {string.Join(", ", availableComponents)}"
                : " No components exist in this circuit.";
            throw new ArgumentException($"Component '{componentName}' not found in circuit '{circuit.Id}'.{componentList}");
        }

        // Get the component definition
        if (!circuit.ComponentDefinitions.TryGetValue(componentName, out var componentDef))
        {
            throw new ArgumentException($"Component definition for '{componentName}' not found.");
        }

        // Process each parameter
        foreach (var param in parameters)
        {
            var paramName = param.Key;
            var paramValue = param.Value;

            if (paramName == "value")
            {
                // Handle value parameter - convert to appropriate component-specific parameter
                if (paramValue is not double doubleValue && !double.TryParse(paramValue?.ToString(), out doubleValue))
                {
                    throw new ArgumentException($"Parameter 'value' must be a numeric value. Got: {paramValue}");
                }

                ModifyComponentValue(circuit, componentName, componentDef, component, doubleValue);
            }
            else
            {
                // Handle other parameters (e.g., "ac", "acphase", etc.)
                // Map "ac" to "acmag" for SpiceSharp compatibility (same as ComponentFactory)
                var actualParamName = paramName;
                if (paramName.Equals("ac", StringComparison.OrdinalIgnoreCase))
                {
                    actualParamName = "acmag";
                }

                if (paramValue is double doubleParam)
                {
                    try
                    {
                        component.SetParameter(actualParamName, doubleParam);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"Failed to set parameter '{paramName}' on component '{componentName}': {ex.Message}");
                    }

                    // Update ComponentDefinition parameters to keep in sync
                    // Store as "ac" in ComponentDefinition for consistency with add_component
                    if (componentDef.Parameters == null)
                    {
                        componentDef.Parameters = new Dictionary<string, object>();
                    }
                    componentDef.Parameters[paramName] = doubleParam;
                }
                else if (paramValue is string stringParam)
                {
                    try
                    {
                        component.SetParameter(paramName, stringParam);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"Failed to set parameter '{paramName}' on component '{componentName}': {ex.Message}");
                    }

                    // Update ComponentDefinition parameters
                    if (componentDef.Parameters == null)
                    {
                        componentDef.Parameters = new Dictionary<string, object>();
                    }
                    componentDef.Parameters[paramName] = stringParam;
                }
                else
                {
                    throw new ArgumentException($"Parameter '{paramName}' has unsupported type: {paramValue?.GetType().Name ?? "null"}");
                }
            }
        }

        // Update modified timestamp
        circuit.ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Modifies a component's value parameter by updating the appropriate component-specific parameter
    /// </summary>
    private void ModifyComponentValue(CircuitModel circuit, string componentName, ComponentDefinition componentDef, IEntity component, double newValue)
    {
        var componentType = componentDef.ComponentType.ToLower();
        var parameterSet = false;

        // Try to set the parameter based on component type
        if (componentType == "resistor")
        {
            try
            {
                component.SetParameter("resistance", newValue);
                parameterSet = true;
                componentDef.Value = newValue;
            }
            catch { }
        }
        else if (componentType == "capacitor")
        {
            try
            {
                component.SetParameter("capacitance", newValue);
                parameterSet = true;
                componentDef.Value = newValue;
            }
            catch { }
        }
        else if (componentType == "inductor")
        {
            try
            {
                component.SetParameter("inductance", newValue);
                parameterSet = true;
                componentDef.Value = newValue;
            }
            catch { }
        }
        else if (componentType == "voltage_source" || componentType == "current_source")
        {
            try
            {
                component.SetParameter("dc", newValue);
                parameterSet = true;
                componentDef.Value = newValue;
            }
            catch { }
        }

        // If SetParameter didn't work, recreate the component with new value
        if (!parameterSet)
        {
            RecreateComponentWithNewValue(circuit, componentName, componentDef, newValue);
        }
    }

    /// <summary>
    /// Recreates a component with a new value
    /// </summary>
    private void RecreateComponentWithNewValue(CircuitModel circuit, string componentName, ComponentDefinition componentDef, double newValue)
    {
        var oldComponent = GetComponent(circuit, componentName);
        
        if (oldComponent != null)
        {
            circuit.InternalCircuit.Remove(oldComponent);
            // Temporarily remove from definitions to avoid duplicate check
            circuit.ComponentDefinitions.Remove(componentName);
        }

        // Update the definition to match the new value
        componentDef.Value = newValue;

        // Recreate the component
        AddComponent(circuit, componentDef);
    }

    /// <summary>
    /// Validates that both inductors exist in the circuit for mutual inductance.
    /// </summary>
    private void ValidateMutualInductance(CircuitModel circuit, ComponentDefinition definition)
    {
        if (definition.Parameters == null || definition.Parameters.Count == 0)
            return; // ComponentFactory will handle missing parameters

        var inductor1Param = definition.Parameters.FirstOrDefault(p => p.Key.Equals("inductor1", StringComparison.OrdinalIgnoreCase));
        var inductor2Param = definition.Parameters.FirstOrDefault(p => p.Key.Equals("inductor2", StringComparison.OrdinalIgnoreCase));

        if (inductor1Param.Key != null)
        {
            var inductor1 = inductor1Param.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(inductor1))
            {
                var inductor1Entity = GetComponent(circuit, inductor1);
                if (inductor1Entity == null)
                    throw new ArgumentException($"Inductor '{inductor1}' specified in 'inductor1' parameter does not exist in the circuit. Create the inductor first before adding mutual inductance.");
            }
        }

        if (inductor2Param.Key != null)
        {
            var inductor2 = inductor2Param.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(inductor2))
            {
                var inductor2Entity = GetComponent(circuit, inductor2);
                if (inductor2Entity == null)
                    throw new ArgumentException($"Inductor '{inductor2}' specified in 'inductor2' parameter does not exist in the circuit. Create the inductor first before adding mutual inductance.");
            }
        }
    }
}

