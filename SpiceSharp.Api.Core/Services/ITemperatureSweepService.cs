using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing temperature sweep analyses
/// </summary>
public interface ITemperatureSweepService
{
    /// <summary>
    /// Runs a temperature sweep analysis by modifying model parameters based on temperature coefficients
    /// </summary>
    /// <param name="circuit">The circuit to analyze</param>
    /// <param name="startTemp">Starting temperature in Celsius</param>
    /// <param name="stopTemp">Ending temperature in Celsius</param>
    /// <param name="stepTemp">Temperature step in Celsius</param>
    /// <param name="analysisType">Type of analysis to run: "operating-point", "dc", "ac", "transient"</param>
    /// <param name="analysisConfig">Configuration specific to the analysis type</param>
    /// <param name="exports">Signals to export (e.g., "v(node)", "i(component)")</param>
    /// <returns>Temperature sweep results</returns>
    TemperatureSweepResult RunTemperatureSweep(
        CircuitModel circuit,
        double startTemp,
        double stopTemp,
        double stepTemp,
        string analysisType,
        object? analysisConfig,
        IEnumerable<string> exports);
}

