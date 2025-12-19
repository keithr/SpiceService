using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing AC (frequency-domain) analysis.
/// </summary>
public interface IACAnalysisService
{
    /// <summary>
    /// Runs an AC frequency sweep analysis on the specified circuit.
    /// </summary>
    /// <param name="circuit">The circuit to analyze.</param>
    /// <param name="startFrequency">The starting frequency in Hz.</param>
    /// <param name="stopFrequency">The ending frequency in Hz.</param>
    /// <param name="numberOfPoints">The number of frequency points to simulate.</param>
    /// <param name="signals">List of signals to export (e.g., "v(node1)", "i(R1)").</param>
    /// <returns>The result of the AC analysis.</returns>
    ACAnalysisResult RunACAnalysis(
        CircuitModel circuit,
        double startFrequency,
        double stopFrequency,
        int numberOfPoints,
        IEnumerable<string>? signals = null);
}

