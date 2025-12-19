namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Defines a component to be added to a circuit.
/// </summary>
public class ComponentDefinition
{
    /// <summary>
    /// Type of component (e.g., "resistor", "capacitor", "diode").
    /// </summary>
    public string ComponentType { get; set; } = string.Empty;

    /// <summary>
    /// Unique name for the component (e.g., "R1", "C1", "Q1").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Connection nodes for the component.
    /// </summary>
    public List<string> Nodes { get; set; } = new();

    /// <summary>
    /// Primary value for the component (resistance, capacitance, etc.).
    /// </summary>
    public double? Value { get; set; }

    /// <summary>
    /// Model name to use for semiconductor components.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Additional parameters for the component.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

