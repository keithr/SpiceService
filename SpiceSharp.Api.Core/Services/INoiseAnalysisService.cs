using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for performing noise analysis.
/// </summary>
public interface INoiseAnalysisService
{
    /// <summary>
    /// Runs a noise analysis on the specified circuit.
    /// </summary>
    /// <param name="circuit">The circuit to analyze.</param>
    /// <param name="startFrequency">The starting frequency in Hz.</param>
    /// <param name="stopFrequency">The ending frequency in Hz.</param>
    /// <param name="numberOfPoints">The number of frequency points to simulate.</param>
    /// <param name="outputNode">The output node for noise measurement (e.g., "out").</param>
    /// <param name="inputSource">The input source name for noise reference (e.g., "V1").</param>
    /// <returns>The result of the noise analysis.</returns>
    NoiseAnalysisResult RunNoiseAnalysis(
        CircuitModel circuit,
        double startFrequency,
        double stopFrequency,
        int numberOfPoints,
        string outputNode,
        string inputSource);
}

