using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing parameter sweep analyses
/// </summary>
public interface IParameterSweepService
{
    /// <summary>
    /// Run a parameter sweep analysis
    /// </summary>
    /// <param name="circuit">The circuit to analyze</param>
    /// <param name="parameterPath">Path to the parameter to sweep (e.g., "R1.value" or "LED_MODEL.IS")</param>
    /// <param name="start">Starting value for the parameter</param>
    /// <param name="stop">Ending value for the parameter</param>
    /// <param name="step">Step size for the parameter</param>
    /// <param name="analysisType">Type of analysis to run ("operating-point", "dc", "ac", "transient")</param>
    /// <param name="analysisConfig">Configuration specific to the analysis type</param>
    /// <param name="exports">List of signals to export</param>
    /// <returns>Parameter sweep results</returns>
    ParameterSweepResult RunParameterSweep(
        CircuitModel circuit,
        string parameterPath,
        double start,
        double stop,
        double step,
        string analysisType,
        object? analysisConfig,
        IEnumerable<string> exports);
}

