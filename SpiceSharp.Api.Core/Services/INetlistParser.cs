using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for parsing SPICE netlists
/// </summary>
public interface INetlistParser
{
    /// <summary>
    /// Parses a SPICE netlist string and extracts components and models
    /// </summary>
    /// <param name="netlist">The SPICE netlist text</param>
    /// <returns>Parsed netlist result with components and models</returns>
    ParsedNetlist ParseNetlist(string netlist);
}

/// <summary>
/// Result of parsing a SPICE netlist
/// </summary>
public class ParsedNetlist
{
    /// <summary>
    /// Parsed component definitions
    /// </summary>
    public List<ComponentDefinition> Components { get; set; } = new();

    /// <summary>
    /// Parsed model definitions
    /// </summary>
    public List<ModelDefinition> Models { get; set; } = new();
}
