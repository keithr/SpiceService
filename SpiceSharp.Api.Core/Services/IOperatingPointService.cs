using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing DC operating point analysis.
/// </summary>
public interface IOperatingPointService
{
    /// <summary>
    /// Runs a DC operating point analysis on the specified circuit.
    /// </summary>
    /// <param name="circuit">The circuit to analyze.</param>
    /// <param name="includePower">Whether to include power dissipation for each component.</param>
    /// <returns>The result of the operating point analysis.</returns>
    OperatingPointResult RunOperatingPointAnalysis(CircuitModel circuit, bool includePower = true);
}

