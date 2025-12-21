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

    /// <summary>
    /// Metadata extracted from comment lines before the .SUBCKT statement.
    /// Contains fields like MANUFACTURER, PART_NUMBER, TYPE, DIAMETER, IMPEDANCE, POWER_RMS, SENSITIVITY, PRICE.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Thiele-Small (T/S) parameters extracted from comment lines before the .SUBCKT statement.
    /// Contains parameters like FS, QTS, QES, QMS, VAS, RE, LE, BL, XMAX, MMS, CMS, SD.
    /// </summary>
    public Dictionary<string, double> TsParameters { get; set; } = new();
}

