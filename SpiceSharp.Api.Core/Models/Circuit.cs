using SpiceSharp;

namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Represents a circuit with components, models, and analysis history.
/// </summary>
public class CircuitModel
{
    /// <summary>
    /// Unique identifier for the circuit.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the circuit.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this circuit is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp when the circuit was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the circuit was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Internal SpiceSharp circuit object.
    /// </summary>
    internal SpiceSharp.Circuit InternalCircuit { get; set; } = new();

    /// <summary>
    /// Gets the SpiceSharp circuit for rendering and analysis.
    /// </summary>
    public SpiceSharp.Circuit GetSpiceSharpCircuit() => InternalCircuit;

    /// <summary>
    /// Stored component definitions for export and metadata tracking.
    /// Key: Component name, Value: Component definition
    /// </summary>
    internal Dictionary<string, ComponentDefinition> ComponentDefinitions { get; set; } = new();

    /// <summary>
    /// Stored model definitions for export and metadata tracking.
    /// Key: Model name, Value: Model definition
    /// </summary>
    internal Dictionary<string, ModelDefinition> ModelDefinitions { get; set; } = new();

    /// <summary>
    /// Number of components in the circuit.
    /// </summary>
    public int ComponentCount => InternalCircuit.Count;

    /// <summary>
    /// All nodes in the circuit.
    /// Extracts unique nodes from all component definitions.
    /// </summary>
    public IEnumerable<string> Nodes
    {
        get
        {
            var nodeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Extract nodes from all component definitions
            foreach (var component in ComponentDefinitions.Values)
            {
                if (component.Nodes != null)
                {
                    foreach (var node in component.Nodes)
                    {
                        if (!string.IsNullOrWhiteSpace(node))
                        {
                            nodeSet.Add(node);
                        }
                    }
                }
            }
            
            return nodeSet;
        }
    }

    /// <summary>
    /// Whether the circuit has a ground node (node "0").
    /// </summary>
    public bool HasGround => Nodes.Contains("0");
}
