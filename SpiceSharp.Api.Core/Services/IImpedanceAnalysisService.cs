using SpiceSharp.Api.Core.Models;

namespace SpiceSharp.Api.Core.Services;

/// <summary>
/// Service for calculating circuit impedance
/// </summary>
public interface IImpedanceAnalysisService
{
    /// <summary>
    /// Calculates impedance at a port by inserting a 1A AC current source and measuring voltage
    /// </summary>
    /// <param name="circuit">The circuit to analyze</param>
    /// <param name="portPositive">Positive terminal node of the port</param>
    /// <param name="portNegative">Negative terminal node of the port (usually "0" for ground)</param>
    /// <param name="startFrequency">Start frequency in Hz</param>
    /// <param name="stopFrequency">Stop frequency in Hz</param>
    /// <param name="numberOfPoints">Number of frequency points</param>
    /// <returns>Impedance analysis result with magnitude and phase vs frequency</returns>
    ImpedanceAnalysisResult CalculateImpedance(
        CircuitModel circuit,
        string portPositive,
        string portNegative,
        double startFrequency,
        double stopFrequency,
        int numberOfPoints);
}
