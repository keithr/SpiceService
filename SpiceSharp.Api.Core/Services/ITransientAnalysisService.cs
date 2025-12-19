using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing transient (time-domain) analysis.
/// </summary>
public interface ITransientAnalysisService
{
    /// <summary>
    /// Runs a transient analysis on the specified circuit.
    /// </summary>
    /// <param name="circuit">The circuit to analyze.</param>
    /// <param name="startTime">The starting time in seconds.</param>
    /// <param name="stopTime">The ending time in seconds.</param>
    /// <param name="maxStep">The maximum time step in seconds.</param>
    /// <param name="signals">List of signals to export (e.g., "v(node1)", "i(R1)").</param>
    /// <param name="useInitialConditions">If true, use initial conditions specified on components (UIC mode). If false, calculate DC operating point first.</param>
    /// <returns>The result of the transient analysis.</returns>
    TransientAnalysisResult RunTransientAnalysis(
        CircuitModel circuit,
        double startTime,
        double stopTime,
        double maxStep,
        IEnumerable<string>? signals = null,
        bool useInitialConditions = false);
}

