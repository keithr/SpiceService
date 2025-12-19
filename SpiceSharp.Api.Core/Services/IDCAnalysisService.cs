using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing DC sweep analysis.
/// </summary>
public interface IDCAnalysisService
{
    /// <summary>
    /// Runs a DC sweep analysis on the specified circuit.
    /// </summary>
    /// <param name="circuit">The circuit to analyze.</param>
    /// <param name="sourceName">The name of the independent source to sweep (e.g., "V1").</param>
    /// <param name="startValue">The starting value for the sweep.</param>
    /// <param name="stopValue">The ending value for the sweep.</param>
    /// <param name="stepValue">The step size for the sweep.</param>
    /// <param name="exports">List of signals to export (e.g., "v(node1)", "i(R1)", "p(V1)").</param>
    /// <returns>The result of the DC sweep analysis.</returns>
    DCAnalysisResult RunDCAnalysis(
        CircuitModel circuit, 
        string sourceName, 
        double startValue, 
        double stopValue, 
        double stepValue,
        IEnumerable<string>? exports = null);
}

