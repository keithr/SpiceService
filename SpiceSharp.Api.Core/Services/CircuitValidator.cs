using SpiceSharp.Api.Core.Models;
using SpiceSharp.Components;
using SpiceSharp.Entities;

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

        // Check for subcircuit issues
        ValidateSubcircuits(circuit, result);

        // Check for DC paths (important for AC analysis)
        ValidateDCPaths(circuit, result);

        // Check for shorts (will be implemented when we have better access to circuit topology)

        return result;
    }

    /// <summary>
    /// Validates that the circuit doesn't have floating nodes.
    /// A floating node is a node that is only connected to one component terminal.
    /// </summary>
    private void ValidateFloatingNodes(CircuitModel circuit, CircuitValidationResult result)
    {
        if (circuit == null || circuit.ComponentDefinitions == null)
            return;

        // Count how many times each node appears in component connections
        var nodeConnectionCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var component in circuit.ComponentDefinitions.Values)
        {
            if (component.Nodes != null)
            {
                foreach (var node in component.Nodes)
                {
                    if (!string.IsNullOrWhiteSpace(node))
                    {
                        if (!nodeConnectionCount.ContainsKey(node))
                        {
                            nodeConnectionCount[node] = 0;
                        }
                        nodeConnectionCount[node]++;
                    }
                }
            }
        }

        // Ground node (0) should always be present and connected
        if (!nodeConnectionCount.ContainsKey("0"))
        {
            result.AddWarning("Ground node (0) is not explicitly connected. This may cause simulation issues.");
        }

        // Check for nodes that appear only once (floating nodes)
        // Note: Ground node can appear once if it's only connected to one component
        // Also note: Voltage source terminals are expected to be floating (one terminal connected)
        foreach (var (node, count) in nodeConnectionCount)
        {
            if (count == 1 && !node.Equals("0", StringComparison.OrdinalIgnoreCase))
            {
                // Check if this node is part of a voltage source (voltage sources have floating terminals by design)
                var isVoltageSourceTerminal = circuit.ComponentDefinitions.Values
                    .Any(c => c.ComponentType?.Equals("voltage_source", StringComparison.OrdinalIgnoreCase) == true &&
                              c.Nodes != null && c.Nodes.Contains(node, StringComparer.OrdinalIgnoreCase));
                
                if (!isVoltageSourceTerminal)
                {
                    result.AddWarning($"Node '{node}' appears to be floating (only connected to one component terminal). " +
                        "This may cause SpiceSharp validation rule violations.");
                }
            }
        }
    }

    /// <summary>
    /// Validates subcircuit instances in the circuit.
    /// </summary>
    private void ValidateSubcircuits(CircuitModel circuit, CircuitValidationResult result)
    {
        var spiceCircuit = circuit.GetSpiceSharpCircuit();
        
        // Use reflection to access ComponentDefinitions
        var componentDefinitionsProp = typeof(CircuitModel).GetProperty("ComponentDefinitions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var componentDefinitions = componentDefinitionsProp?.GetValue(circuit) as Dictionary<string, ComponentDefinition>;
        
        if (componentDefinitions == null)
            return;
        
        // Find all subcircuit instances in the circuit
        foreach (var entity in spiceCircuit)
        {
            if (entity is Subcircuit subcircuitInstance)
            {
                var instanceName = subcircuitInstance.Name;
                
                // Get the component definition for this instance
                if (!componentDefinitions.TryGetValue(instanceName, out var componentDef))
                {
                    result.AddError($"Subcircuit instance '{instanceName}' has no component definition.");
                    continue;
                }
                
                // Check if the component definition specifies a model (subcircuit name)
                if (string.IsNullOrWhiteSpace(componentDef.Model))
                {
                    result.AddError($"Subcircuit instance '{instanceName}' does not specify a model (subcircuit definition name).");
                    continue;
                }
                
                var subcircuitDefinitionName = componentDef.Model;
                
                // Check if the subcircuit definition exists in the circuit
                if (!spiceCircuit.TryGetEntity(subcircuitDefinitionName, out var definitionEntity))
                {
                    result.AddError($"Subcircuit definition '{subcircuitDefinitionName}' not found for instance '{instanceName}'. " +
                        $"The definition must be registered in the circuit before creating instances.");
                    continue;
                }
                
                // Get the ISubcircuitDefinition from the entity
                ISubcircuitDefinition? subcircuitDef = null;
                if (definitionEntity is ISubcircuitDefinition directDef)
                {
                    subcircuitDef = directDef;
                }
                else
                {
                    // Try to get it via the Definition property (for SubcircuitDefinitionEntity wrapper)
                    var definitionProp = definitionEntity.GetType().GetProperty("Definition", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    subcircuitDef = definitionProp?.GetValue(definitionEntity) as ISubcircuitDefinition;
                }
                
                if (subcircuitDef == null)
                {
                    result.AddError($"Subcircuit definition '{subcircuitDefinitionName}' found but could not be accessed for instance '{instanceName}'.");
                    continue;
                }
                
                // Check node count match
                // ISubcircuitDefinition has Pins (pin names), not Nodes
                var definitionPinCount = subcircuitDef.Pins?.Count ?? 0;
                var instanceNodeCount = componentDef.Nodes?.Count ?? 0;
                
                if (definitionPinCount != instanceNodeCount)
                {
                    result.AddError($"Subcircuit instance '{instanceName}' has {instanceNodeCount} node(s), " +
                        $"but definition '{subcircuitDefinitionName}' expects {definitionPinCount} pin(s).");
                }
            }
        }
    }

    /// <summary>
    /// Validates that all nodes have DC paths to ground (required for AC analysis).
    /// This is a simplified check - SpiceSharp will do the actual validation.
    /// </summary>
    private void ValidateDCPaths(CircuitModel circuit, CircuitValidationResult result)
    {
        if (circuit == null || !circuit.HasGround)
        {
            result.AddWarning("Circuit does not have a ground node. AC analysis requires all nodes to have DC paths to ground.");
            return;
        }

        // For AC analysis, SpiceSharp requires all nodes to have DC paths to ground.
        // This means:
        // - Capacitors block DC, so nodes connected only through capacitors may not have DC paths
        // - Inductors have DC resistance, but pure inductor loops need resistance
        // - Subcircuits may have internal nodes without DC paths
        
        // We can't fully validate DC paths without analyzing the circuit topology,
        // but we can warn about common issues:
        var nodes = circuit.Nodes.ToList();
        var hasVoltageSource = circuit.ComponentDefinitions.Values
            .Any(c => c.ComponentType?.Equals("voltage_source", StringComparison.OrdinalIgnoreCase) == true ||
                      c.ComponentType?.Equals("voltage_switch", StringComparison.OrdinalIgnoreCase) == true);

        if (!hasVoltageSource)
        {
            result.AddWarning("Circuit does not appear to have a voltage source. AC analysis requires an AC voltage source.");
        }

        // Note: Full DC path validation would require graph traversal, which is complex.
        // SpiceSharp will perform the actual validation and provide rule violations if needed.
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

