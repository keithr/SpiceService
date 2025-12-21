namespace SpiceSharp.Api.Core.Models;

/// <summary>
/// Defines a subcircuit definition from a SPICE library file.
/// </summary>
public class SubcircuitDefinition
{
    /// <summary>
    /// Unique name for the subcircuit (e.g., "irf1010n", "opamp_ideal").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// External node names for the subcircuit (connection points).
    /// </summary>
    public List<string> Nodes { get; set; } = new();

    /// <summary>
    /// Internal netlist definition of the subcircuit (all lines between .SUBCKT and .ENDS).
    /// </summary>
    public string Definition { get; set; } = string.Empty;
}

