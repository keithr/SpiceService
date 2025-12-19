using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for exporting circuits to SPICE netlist format.
/// </summary>
public interface INetlistService
{
    /// <summary>
    /// Exports a circuit to SPICE netlist format.
    /// </summary>
    /// <param name="circuit">The circuit to export</param>
    /// <param name="includeComments">Whether to include descriptive comments</param>
    /// <returns>The SPICE netlist as a string</returns>
    string ExportNetlist(CircuitModel circuit, bool includeComments = true);
}

