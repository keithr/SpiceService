using SpiceSharp;
using SpiceSharp.Entities;
using SpiceSharp.Components;
using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for managing components within circuits.
/// </summary>
public class ComponentService : IComponentService
{
    private readonly ComponentFactory _factory;
    private readonly ILibraryService? _libraryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentService"/> class.
    /// </summary>
    /// <param name="libraryService">Optional library service for loading subcircuit definitions</param>
    public ComponentService(ILibraryService? libraryService = null)
    {
        _factory = new ComponentFactory();
        _libraryService = libraryService;
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

        // Special handling for subcircuits - need to load definition from library
        if (definition.ComponentType.Equals("subcircuit", StringComparison.OrdinalIgnoreCase))
        {
            return AddSubcircuitComponent(circuit, definition);
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

    /// <summary>
    /// Adds a subcircuit component, loading the subcircuit definition from the library if needed.
    /// </summary>
    private IEntity AddSubcircuitComponent(CircuitModel circuit, ComponentDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Model))
            throw new ArgumentException(
                $"Subcircuit component '{definition.Name}' requires a model (subcircuit name) to be specified. " +
                $"Set the 'model' parameter to the name of the subcircuit definition (e.g., from library_search results).");

        if (definition.Nodes == null || definition.Nodes.Count == 0)
            throw new ArgumentException(
                $"Subcircuit component '{definition.Name}' requires at least one connection node. " +
                $"Specify the 'nodes' array with the connection points for this subcircuit instance.");

        var subcircuitName = definition.Model;

        // Check if subcircuit definition already exists in circuit
        ISubcircuitDefinition? existingDefinition = null;
        // Try to get existing definition from circuit
        // Definitions are stored as SubcircuitDefinitionEntity wrappers
        if (circuit.InternalCircuit.TryGetEntity(subcircuitName, out var existingEntity))
        {
            if (existingEntity is SubcircuitDefinitionEntity wrapper)
            {
                existingDefinition = wrapper.Definition;
            }
            else if (existingEntity is ISubcircuitDefinition existingSubDef)
            {
                // Fallback for direct ISubcircuitDefinition (shouldn't happen, but handle it)
                existingDefinition = existingSubDef;
            }
        }

        // If definition doesn't exist, try to load from library
        if (existingDefinition == null && _libraryService != null)
        {
            var librarySubcircuit = _libraryService.GetSubcircuitByName(subcircuitName);
            if (librarySubcircuit != null)
            {
                // Create SpiceSharp subcircuit definition from library definition
                var subcircuitDef = CreateSpiceSharpSubcircuitDefinition(librarySubcircuit);
                
                // Register the definition with the circuit before creating instances
                // The definition must be in the circuit for SpiceSharp to find it by name
                // Use reflection to set the name if it's stored in a private field
                // SpiceSharp's SubcircuitDefinition needs to be registered with a name
                // so it can be retrieved via TryGetEntity
                try
                {
                    // Try to set the name using reflection (Name property is read-only)
                    var nameField = subcircuitDef.GetType().GetField("_name", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (nameField != null)
                    {
                        nameField.SetValue(subcircuitDef, subcircuitName);
                    }
                    else
                    {
                        // Try property with private setter
                        var nameProperty = subcircuitDef.GetType().GetProperty("Name",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (nameProperty?.SetMethod != null)
                        {
                            nameProperty.SetValue(subcircuitDef, subcircuitName);
                        }
                    }
                }
                catch
                {
                    // If reflection fails, we'll try adding it anyway
                    // SpiceSharp might handle it differently
                }
                
                // Register the definition with the circuit
                // Since SubcircuitDefinition doesn't implement IEntity, we need to create a wrapper
                // that can be stored in the circuit and retrieved by name
                // We'll create a simple entity that wraps the definition
                var definitionWrapper = new SubcircuitDefinitionEntity(subcircuitName, subcircuitDef);
                circuit.InternalCircuit.Add(definitionWrapper);
                existingDefinition = subcircuitDef;
            }
            else
            {
                throw new ArgumentException(
                    $"Subcircuit component '{definition.Name}' references definition '{subcircuitName}' which was not found in the library. " +
                    $"Use library_search to find available subcircuits, or define the subcircuit before instantiating it. " +
                    $"Component nodes: [{string.Join(", ", definition.Nodes ?? new List<string>())}]");
            }
        }
        else if (existingDefinition == null)
        {
            throw new ArgumentException(
                $"Subcircuit component '{definition.Name}' references definition '{subcircuitName}' which was not found in the circuit, " +
                $"and library service is not available. Define the subcircuit before instantiating it, or configure LibraryService. " +
                $"Component nodes: [{string.Join(", ", definition.Nodes ?? new List<string>())}]");
        }

        // Now create the subcircuit instance
        // SpiceSharp.Subcircuit constructor: Subcircuit(name, subcircuitDefinitionName, nodes...)
        if (existingDefinition == null)
        {
            throw new ArgumentException(
                $"Subcircuit component '{definition.Name}' could not be created: definition '{subcircuitName}' was not found after loading from library. " +
                $"This is an internal error - please report it. Component nodes: [{string.Join(", ", definition.Nodes ?? new List<string>())}]");
        }
        
        // Validate node count matches definition pin count
        var definitionPinCount = existingDefinition.Pins?.Count ?? 0;
        var instanceNodeCount = definition.Nodes?.Count ?? 0;
        if (definitionPinCount != instanceNodeCount)
        {
            var componentNodes = definition.Nodes != null ? string.Join(", ", definition.Nodes) : "none";
            var definitionPins = existingDefinition.Pins != null ? string.Join(", ", existingDefinition.Pins) : "none";
            throw new ArgumentException(
                $"Subcircuit component '{definition.Name}' has {instanceNodeCount} node(s), " +
                $"but definition '{subcircuitName}' expects {definitionPinCount} pin(s). " +
                $"Component nodes: [{componentNodes}]. " +
                $"Definition pins: [{definitionPins}]. " +
                $"Ensure the number of nodes matches the number of pins in the subcircuit definition.");
        }
        
        // definition.Nodes is already validated above, but compiler doesn't know that
        var nodesArray = definition.Nodes?.ToArray() ?? Array.Empty<string>();
        var subcircuitInstance = new Subcircuit(
            definition.Name,
            existingDefinition, // Use the definition from the circuit
            nodesArray
        );

        // Store the definition for export and metadata tracking
        circuit.ComponentDefinitions[definition.Name] = definition;

        // Add to the circuit
        circuit.InternalCircuit.Add(subcircuitInstance);

        // Update modified timestamp
        circuit.ModifiedAt = DateTime.UtcNow;

        return subcircuitInstance;
    }

    /// <summary>
    /// Creates a SpiceSharp SubcircuitDefinition entity from a library SubcircuitDefinition.
    /// </summary>
    private SpiceSharp.Components.SubcircuitDefinition CreateSpiceSharpSubcircuitDefinition(Models.SubcircuitDefinition libraryDef)
    {
        // Parse the subcircuit definition into components using our NetlistParser
        var netlistParser = new NetlistParser();
        var parsedNetlist = netlistParser.ParseNetlist(libraryDef.Definition);
        
        // Create a temporary circuit to hold the subcircuit's internal components
        var subcircuitCircuit = new Circuit();
        
        // Add all components from the parsed definition to the subcircuit circuit
        var componentFactory = new ComponentFactory();
        foreach (var componentDef in parsedNetlist.Components)
        {
            try
            {
                var entity = componentFactory.CreateComponent(componentDef);
                subcircuitCircuit.Add(entity);
            }
            catch (Exception ex)
            {
                // Log but continue - some components might not be supported yet
                System.Diagnostics.Debug.WriteLine($"Warning: Could not add component {componentDef.Name} to subcircuit: {ex.Message}");
            }
        }
        
        // Add all models to the subcircuit circuit
        var modelService = new ModelService();
        foreach (var modelDef in parsedNetlist.Models)
        {
            try
            {
                var modelEntity = modelService.CreateModel(modelDef);
                subcircuitCircuit.Add(modelEntity);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Could not add model {modelDef.ModelName} to subcircuit: {ex.Message}");
            }
        }
        
        // Create the SpiceSharp SubcircuitDefinition
        // SpiceSharp.Components.SubcircuitDefinition constructor: SubcircuitDefinition(circuit, nodes)
        // The name is set when the definition is added to the circuit
        var subcircuitDef = new SpiceSharp.Components.SubcircuitDefinition(
            subcircuitCircuit,
            libraryDef.Nodes.ToArray()
        );
        
        return subcircuitDef;
    }
}

